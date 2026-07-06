using ERP.Application.Common.Interfaces;
using ERP.Domain.Common;
using ERP.Shared.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ERP.Application.Features.Sales.Invoices;

// ---- Issue (commits the tax document + attempts fiscalization) ----
public sealed record IssueInvoiceCommand(Guid Id) : IRequest<Result>;

public sealed class IssueInvoiceCommandHandler : IRequestHandler<IssueInvoiceCommand, Result>
{
    private readonly IApplicationDbContext _db;
    private readonly IFiscalizationService _fiscalization;
    private readonly IRealtimeNotifier _notifications;
    private readonly IEmailQueue _email;

    public IssueInvoiceCommandHandler(
        IApplicationDbContext db, IFiscalizationService fiscalization,
        IRealtimeNotifier notifications, IEmailQueue email)
    {
        _db = db;
        _fiscalization = fiscalization;
        _notifications = notifications;
        _email = email;
    }

    public async Task<Result> Handle(IssueInvoiceCommand request, CancellationToken ct)
    {
        var invoice = await _db.Invoices.Include(i => i.Lines)
            .FirstOrDefaultAsync(i => i.Id == request.Id, ct);
        if (invoice is null) return Result.Failure(Error.NotFound("Invoice not found."));

        try { invoice.Issue(); }
        catch (DomainException ex) { return Result.Failure(Error.Conflict(ex.Message)); }

        // Submit to FRCS/VMS. The stub records NotSubmitted; a verified adapter would return
        // Submitted with an accreditation reference. Either way the outcome is persisted.
        var fiscal = await _fiscalization.SubmitAsync(invoice, ct);
        invoice.SetFiscalResult(fiscal.Status, fiscal.Reference);

        await _db.SaveChangesAsync(ct);

        // Notify staff in real time and queue an email to the customer (if we have an address).
        await _notifications.PublishToAllAsync(
            new NotificationMessage("Invoice issued", $"Invoice {invoice.Number} issued for {invoice.Total:0.00}."), ct);

        var customerEmail = await _db.Customers
            .Where(c => c.Id == invoice.CustomerId)
            .Select(c => c.Email)
            .FirstOrDefaultAsync(ct);
        if (!string.IsNullOrWhiteSpace(customerEmail))
            _email.Enqueue(new EmailMessage(customerEmail,
                $"Invoice {invoice.Number}", $"Your invoice for {invoice.Total:0.00} has been issued."));

        return Result.Success();
    }
}

// ---- Void ----
public sealed record VoidInvoiceCommand(Guid Id) : IRequest<Result>;

public sealed class VoidInvoiceCommandHandler : IRequestHandler<VoidInvoiceCommand, Result>
{
    private readonly IApplicationDbContext _db;
    public VoidInvoiceCommandHandler(IApplicationDbContext db) => _db = db;

    public async Task<Result> Handle(VoidInvoiceCommand request, CancellationToken ct)
    {
        var invoice = await _db.Invoices.FirstOrDefaultAsync(i => i.Id == request.Id, ct);
        if (invoice is null) return Result.Failure(Error.NotFound("Invoice not found."));

        try { invoice.Void(); }
        catch (DomainException ex) { return Result.Failure(Error.Conflict(ex.Message)); }

        await _db.SaveChangesAsync(ct);
        return Result.Success();
    }
}
