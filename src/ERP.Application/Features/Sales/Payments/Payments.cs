using ERP.Application.Common.Interfaces;
using ERP.Domain.Common;
using ERP.Domain.Sales;
using ERP.Shared.Results;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ERP.Application.Features.Sales.Payments;

public sealed record PaymentDto(
    Guid Id, string Number, Guid InvoiceId, decimal Amount, DateOnly PaymentDate,
    PaymentMethod Method, string? Reference);

// ---- Record a payment against an invoice ----
public sealed record RecordPaymentCommand(
    Guid InvoiceId, decimal Amount, DateOnly PaymentDate, PaymentMethod Method, string? Reference)
    : IRequest<Result<Guid>>;

public sealed class RecordPaymentCommandValidator : AbstractValidator<RecordPaymentCommand>
{
    public RecordPaymentCommandValidator()
    {
        RuleFor(x => x.InvoiceId).NotEmpty();
        RuleFor(x => x.Amount).GreaterThan(0);
        RuleFor(x => x.PaymentDate).NotEmpty();
        RuleFor(x => x.Method).IsInEnum();
        RuleFor(x => x.Reference).MaximumLength(128);
    }
}

public sealed class RecordPaymentCommandHandler : IRequestHandler<RecordPaymentCommand, Result<Guid>>
{
    private readonly IApplicationDbContext _db;
    public RecordPaymentCommandHandler(IApplicationDbContext db) => _db = db;

    public async Task<Result<Guid>> Handle(RecordPaymentCommand request, CancellationToken ct)
    {
        var invoice = await _db.Invoices.Include(i => i.Lines)
            .FirstOrDefaultAsync(i => i.Id == request.InvoiceId, ct);
        if (invoice is null)
            return Result.Failure<Guid>(Error.NotFound("Invoice not found."));

        // ApplyPayment enforces status + overpayment rules and advances the invoice status.
        try { invoice.ApplyPayment(request.Amount); }
        catch (DomainException ex) { return Result.Failure<Guid>(Error.Conflict(ex.Message)); }

        var sequence = await _db.Payments.IgnoreQueryFilters().CountAsync(ct) + 1;
        var payment = new Payment
        {
            Number = ERP.Application.Features.Sales.DocumentNumber.For("PAY", sequence),
            InvoiceId = invoice.Id,
            CustomerId = invoice.CustomerId,
            Amount = request.Amount,
            PaymentDate = request.PaymentDate,
            Method = request.Method,
            Reference = request.Reference
        };
        _db.Payments.Add(payment);

        await _db.SaveChangesAsync(ct);
        return Result.Success(payment.Id);
    }
}

// ---- List payments for an invoice ----
public sealed record GetPaymentsByInvoiceQuery(Guid InvoiceId) : IRequest<Result<IReadOnlyList<PaymentDto>>>;

public sealed class GetPaymentsByInvoiceQueryHandler
    : IRequestHandler<GetPaymentsByInvoiceQuery, Result<IReadOnlyList<PaymentDto>>>
{
    private readonly IApplicationDbContext _db;
    public GetPaymentsByInvoiceQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<Result<IReadOnlyList<PaymentDto>>> Handle(GetPaymentsByInvoiceQuery request, CancellationToken ct)
    {
        var items = await _db.Payments.AsNoTracking()
            .Where(p => p.InvoiceId == request.InvoiceId)
            .OrderBy(p => p.PaymentDate)
            .Select(p => new PaymentDto(p.Id, p.Number, p.InvoiceId, p.Amount, p.PaymentDate, p.Method, p.Reference))
            .ToListAsync(ct);

        return Result.Success<IReadOnlyList<PaymentDto>>(items);
    }
}
