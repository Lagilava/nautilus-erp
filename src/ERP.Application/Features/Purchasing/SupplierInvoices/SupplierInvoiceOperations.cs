using ERP.Application.Common.Interfaces;
using ERP.Application.Common.Models;
using ERP.Application.Common.Security;
using ERP.Application.Features.Sales;
using ERP.Domain.Common;
using ERP.Domain.Purchasing;
using ERP.Domain.Sales;
using ERP.Shared.Results;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ERP.Application.Features.Purchasing.SupplierInvoices;

// ---- Approve / Cancel ----
public sealed record ApproveSupplierInvoiceCommand(Guid Id) : IRequest<Result>;
public sealed record CancelSupplierInvoiceCommand(Guid Id) : IRequest<Result>;

public sealed class ApproveSupplierInvoiceCommandHandler : IRequestHandler<ApproveSupplierInvoiceCommand, Result>
{
    private readonly IApplicationDbContext _db;
    private readonly ISegregationOfDuties _sod;
    private readonly ICurrentUserService _currentUser;

    public ApproveSupplierInvoiceCommandHandler(
        IApplicationDbContext db, ISegregationOfDuties sod, ICurrentUserService currentUser)
    {
        _db = db;
        _sod = sod;
        _currentUser = currentUser;
    }

    public async Task<Result> Handle(ApproveSupplierInvoiceCommand request, CancellationToken ct)
    {
        var inv = await _db.SupplierInvoices.Include(i => i.Lines).FirstOrDefaultAsync(i => i.Id == request.Id, ct);
        if (inv is null) return Result.Failure(Error.NotFound("Supplier invoice not found."));

        // Whoever entered the bill, or confirmed the goods it bills for, may not approve it —
        // otherwise one person completes the three-way match alone.
        var receivers = inv.PurchaseOrderId is { } poId
            ? await _db.GoodsReceipts.AsNoTracking()
                .Where(g => g.PurchaseOrderId == poId)
                .Select(g => g.CreatedBy)
                .ToArrayAsync(ct)
            : [];

        var sod = _sod.Ensure(SoDRule.SupplierInvoiceApproval,
            "You cannot approve a supplier invoice you entered, or one for goods you received.",
            [inv.CreatedBy, .. receivers]);
        if (sod.IsFailure) return sod;

        try { inv.Approve(_currentUser.UserId?.ToString()); }
        catch (DomainException ex) { return Result.Failure(Error.Conflict(ex.Message)); }
        await _db.SaveChangesAsync(ct);
        return Result.Success();
    }
}

public sealed class CancelSupplierInvoiceCommandHandler : IRequestHandler<CancelSupplierInvoiceCommand, Result>
{
    private readonly IApplicationDbContext _db;
    private readonly ISegregationOfDuties _sod;

    public CancelSupplierInvoiceCommandHandler(IApplicationDbContext db, ISegregationOfDuties sod)
    {
        _db = db;
        _sod = sod;
    }

    public async Task<Result> Handle(CancelSupplierInvoiceCommand request, CancellationToken ct)
    {
        var inv = await _db.SupplierInvoices.FirstOrDefaultAsync(i => i.Id == request.Id, ct);
        if (inv is null) return Result.Failure(Error.NotFound("Supplier invoice not found."));

        // Cancelling an approved bill undoes a colleague's approval. Without this, whoever
        // entered the invoice has a back door out of the four-eyes check on approval.
        if (inv.Status == SupplierInvoiceStatus.Approved)
        {
            var sod = _sod.Ensure(SoDRule.SupplierInvoiceCancel,
                "You cannot cancel a supplier invoice you entered once it has been approved.",
                inv.CreatedBy);
            if (sod.IsFailure) return sod;
        }

        try { inv.Cancel(); }
        catch (DomainException ex) { return Result.Failure(Error.Conflict(ex.Message)); }
        await _db.SaveChangesAsync(ct);
        return Result.Success();
    }
}

// ---- Record a payment to the supplier ----
public sealed record RecordSupplierPaymentCommand(
    Guid SupplierInvoiceId, decimal Amount, DateOnly PaymentDate, PaymentMethod Method, string? Reference)
    : IRequest<Result<Guid>>;

public sealed class RecordSupplierPaymentCommandValidator : AbstractValidator<RecordSupplierPaymentCommand>
{
    public RecordSupplierPaymentCommandValidator()
    {
        RuleFor(x => x.SupplierInvoiceId).NotEmpty();
        RuleFor(x => x.Amount).GreaterThan(0);
        RuleFor(x => x.PaymentDate).NotEmpty();
        RuleFor(x => x.Method).IsInEnum();
        RuleFor(x => x.Reference).MaximumLength(128);
    }
}

public sealed class RecordSupplierPaymentCommandHandler
    : IRequestHandler<RecordSupplierPaymentCommand, Result<Guid>>
{
    private readonly IApplicationDbContext _db;
    private readonly ISegregationOfDuties _sod;

    public RecordSupplierPaymentCommandHandler(IApplicationDbContext db, ISegregationOfDuties sod)
    {
        _db = db;
        _sod = sod;
    }

    public async Task<Result<Guid>> Handle(RecordSupplierPaymentCommand request, CancellationToken ct)
    {
        var invoice = await _db.SupplierInvoices.Include(i => i.Lines)
            .FirstOrDefaultAsync(i => i.Id == request.SupplierInvoiceId, ct);
        if (invoice is null)
            return Result.Failure<Guid>(Error.NotFound("Supplier invoice not found."));

        // Approving a payable and releasing the money are different duties.
        var sod = _sod.Ensure(SoDRule.SupplierPayment,
            "You cannot pay a supplier invoice you approved.",
            invoice.ApprovedBy);
        if (sod.IsFailure) return Result.Failure<Guid>(sod.Error);

        try { invoice.ApplyPayment(request.Amount); }
        catch (DomainException ex) { return Result.Failure<Guid>(Error.Conflict(ex.Message)); }

        var sequence = await _db.SupplierPayments.IgnoreQueryFilters().CountAsync(ct) + 1;
        var payment = new SupplierPayment
        {
            Number = DocumentNumber.For("SPAY", sequence),
            SupplierInvoiceId = invoice.Id,
            SupplierId = invoice.SupplierId,
            Amount = request.Amount,
            PaymentDate = request.PaymentDate,
            Method = request.Method,
            Reference = request.Reference
        };
        _db.SupplierPayments.Add(payment);

        await _db.SaveChangesAsync(ct);
        return Result.Success(payment.Id);
    }
}

// ---- Queries ----
public sealed record SupplierInvoiceLineDto(
    Guid Id, Guid ProductId, string Description, decimal Quantity, decimal UnitCost,
    decimal TaxRate, decimal LineSubTotal, decimal LineTax, decimal LineTotal);

public sealed record SupplierInvoiceDto(
    Guid Id, string Number, Guid SupplierId, Guid? PurchaseOrderId, string? SupplierReference,
    DateOnly IssueDate, DateOnly? DueDate, SupplierInvoiceStatus Status,
    decimal SubTotal, decimal TaxTotal, decimal Total, decimal AmountPaid, decimal Balance,
    IReadOnlyList<SupplierInvoiceLineDto> Lines);

public sealed record SupplierInvoiceSummaryDto(
    Guid Id, string Number, Guid SupplierId, DateOnly IssueDate,
    SupplierInvoiceStatus Status, decimal Total, decimal Balance);

public sealed record GetSupplierInvoiceByIdQuery(Guid Id) : IRequest<Result<SupplierInvoiceDto>>;

public sealed class GetSupplierInvoiceByIdQueryHandler
    : IRequestHandler<GetSupplierInvoiceByIdQuery, Result<SupplierInvoiceDto>>
{
    private readonly IApplicationDbContext _db;
    public GetSupplierInvoiceByIdQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<Result<SupplierInvoiceDto>> Handle(GetSupplierInvoiceByIdQuery request, CancellationToken ct)
    {
        var inv = await _db.SupplierInvoices.AsNoTracking().Include(i => i.Lines)
            .FirstOrDefaultAsync(i => i.Id == request.Id, ct);
        if (inv is null) return Result.Failure<SupplierInvoiceDto>(Error.NotFound("Supplier invoice not found."));

        var dto = new SupplierInvoiceDto(
            inv.Id, inv.Number, inv.SupplierId, inv.PurchaseOrderId, inv.SupplierReference,
            inv.IssueDate, inv.DueDate, inv.Status,
            inv.SubTotal, inv.TaxTotal, inv.Total, inv.AmountPaid, inv.Balance,
            inv.Lines.Select(l => new SupplierInvoiceLineDto(
                l.Id, l.ProductId, l.Description, l.Quantity, l.UnitCost, l.TaxRate,
                l.LineSubTotal, l.LineTax, l.LineTotal)).ToList());
        return Result.Success(dto);
    }
}

public sealed record GetSupplierInvoicesQuery : PagedQuery, IRequest<Result<PagedResult<SupplierInvoiceSummaryDto>>>
{
    public Guid? SupplierId { get; init; }
    public SupplierInvoiceStatus? Status { get; init; }
}

public sealed class GetSupplierInvoicesQueryHandler
    : IRequestHandler<GetSupplierInvoicesQuery, Result<PagedResult<SupplierInvoiceSummaryDto>>>
{
    private readonly IApplicationDbContext _db;
    public GetSupplierInvoicesQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<Result<PagedResult<SupplierInvoiceSummaryDto>>> Handle(GetSupplierInvoicesQuery request, CancellationToken ct)
    {
        var query = _db.SupplierInvoices.AsNoTracking().Include(i => i.Lines).AsQueryable();
        if (request.SupplierId is { } sid) query = query.Where(i => i.SupplierId == sid);
        if (request.Status is { } st) query = query.Where(i => i.Status == st);

        var total = await query.CountAsync(ct);
        var invoices = await query
            .OrderByDescending(i => i.IssueDate)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync(ct);

        var items = invoices
            .Select(i => new SupplierInvoiceSummaryDto(
                i.Id, i.Number, i.SupplierId, i.IssueDate, i.Status, i.Total, i.Balance))
            .ToList();
        return Result.Success(new PagedResult<SupplierInvoiceSummaryDto>(items, request.Page, request.PageSize, total));
    }
}
