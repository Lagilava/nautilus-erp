using ERP.Application.Common.Interfaces;
using ERP.Application.Features.Sales;
using ERP.Domain.Purchasing;
using ERP.Shared.Results;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ERP.Application.Features.Purchasing.SupplierInvoices;

/// <summary>
/// Raises a draft supplier invoice from a purchase order, snapshotting each line's input
/// VAT from the product's tax at the rate in force on the issue date.
/// </summary>
public sealed record CreateSupplierInvoiceFromOrderCommand(
    Guid PurchaseOrderId, DateOnly IssueDate, DateOnly? DueDate, string? SupplierReference)
    : IRequest<Result<Guid>>;

public sealed class CreateSupplierInvoiceFromOrderCommandValidator
    : AbstractValidator<CreateSupplierInvoiceFromOrderCommand>
{
    public CreateSupplierInvoiceFromOrderCommandValidator()
    {
        RuleFor(x => x.PurchaseOrderId).NotEmpty();
        RuleFor(x => x.IssueDate).NotEmpty();
        RuleFor(x => x.DueDate).GreaterThanOrEqualTo(x => x.IssueDate).When(x => x.DueDate.HasValue);
        RuleFor(x => x.SupplierReference).MaximumLength(64);
    }
}

public sealed class CreateSupplierInvoiceFromOrderCommandHandler
    : IRequestHandler<CreateSupplierInvoiceFromOrderCommand, Result<Guid>>
{
    private readonly IApplicationDbContext _db;
    public CreateSupplierInvoiceFromOrderCommandHandler(IApplicationDbContext db) => _db = db;

    public async Task<Result<Guid>> Handle(CreateSupplierInvoiceFromOrderCommand request, CancellationToken ct)
    {
        var order = await _db.PurchaseOrders.Include(o => o.Lines)
            .FirstOrDefaultAsync(o => o.Id == request.PurchaseOrderId, ct);
        if (order is null)
            return Result.Failure<Guid>(Error.NotFound("Purchase order not found."));
        if (order.Status is PurchaseOrderStatus.Draft or PurchaseOrderStatus.Cancelled)
            return Result.Failure<Guid>(Error.Conflict($"Cannot invoice a {order.Status} purchase order."));

        var productIds = order.Lines.Select(l => l.ProductId).Distinct().ToList();
        var products = await _db.Products
            .Where(p => productIds.Contains(p.Id))
            .Select(p => new { p.Id, p.Name, p.TaxId })
            .ToListAsync(ct);
        var taxIds = products.Select(p => p.TaxId).Distinct().ToList();
        var taxes = await _db.Taxes.Include(t => t.Rates).Where(t => taxIds.Contains(t.Id)).ToListAsync(ct);
        var productById = products.ToDictionary(p => p.Id);
        var taxById = taxes.ToDictionary(t => t.Id);

        var sequence = await _db.SupplierInvoices.IgnoreQueryFilters().CountAsync(ct) + 1;
        var invoice = new SupplierInvoice
        {
            Number = DocumentNumber.For("SINV", sequence),
            SupplierId = order.SupplierId,
            PurchaseOrderId = order.Id,
            SupplierReference = request.SupplierReference,
            IssueDate = request.IssueDate,
            DueDate = request.DueDate
        };

        foreach (var line in order.Lines)
        {
            var product = productById[line.ProductId];
            var rate = taxById.TryGetValue(product.TaxId, out var tax) ? tax.GetRateOn(request.IssueDate) : 0m;
            invoice.AddLine(line.ProductId, product.Name, line.Quantity, line.UnitCost, rate);
        }

        _db.SupplierInvoices.Add(invoice);
        await _db.SaveChangesAsync(ct);
        return Result.Success(invoice.Id);
    }
}
