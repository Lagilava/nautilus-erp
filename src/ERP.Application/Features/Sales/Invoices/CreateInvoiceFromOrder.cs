using ERP.Application.Common.Interfaces;
using ERP.Domain.Sales;
using ERP.Shared.Results;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ERP.Application.Features.Sales.Invoices;

/// <summary>
/// Raises a draft invoice from a confirmed/fulfilled sales order. Each line's VAT rate is
/// snapshotted from the product's tax, resolved at the rate in force on the issue date via
/// the effective-dated tax engine — so the invoice is a faithful tax document.
/// </summary>
public sealed record CreateInvoiceFromOrderCommand(Guid SalesOrderId, DateOnly IssueDate, DateOnly? DueDate)
    : IRequest<Result<Guid>>;

public sealed class CreateInvoiceFromOrderCommandValidator : AbstractValidator<CreateInvoiceFromOrderCommand>
{
    public CreateInvoiceFromOrderCommandValidator()
    {
        RuleFor(x => x.SalesOrderId).NotEmpty();
        RuleFor(x => x.IssueDate).NotEmpty();
        RuleFor(x => x.DueDate).GreaterThanOrEqualTo(x => x.IssueDate)
            .When(x => x.DueDate.HasValue);
    }
}

public sealed class CreateInvoiceFromOrderCommandHandler
    : IRequestHandler<CreateInvoiceFromOrderCommand, Result<Guid>>
{
    private readonly IApplicationDbContext _db;
    public CreateInvoiceFromOrderCommandHandler(IApplicationDbContext db) => _db = db;

    public async Task<Result<Guid>> Handle(CreateInvoiceFromOrderCommand request, CancellationToken ct)
    {
        var order = await _db.SalesOrders.Include(o => o.Lines)
            .FirstOrDefaultAsync(o => o.Id == request.SalesOrderId, ct);
        if (order is null)
            return Result.Failure<Guid>(Error.NotFound("Sales order not found."));
        if (order.Status is SalesOrderStatus.Draft or SalesOrderStatus.Cancelled)
            return Result.Failure<Guid>(Error.Conflict($"Cannot invoice a {order.Status} order."));

        // Load the products with their tax + rate history to snapshot each line's VAT.
        var productIds = order.Lines.Select(l => l.ProductId).Distinct().ToList();
        var products = await _db.Products
            .Where(p => productIds.Contains(p.Id))
            .Select(p => new { p.Id, p.Name, p.TaxId })
            .ToListAsync(ct);
        var taxIds = products.Select(p => p.TaxId).Distinct().ToList();
        var taxes = await _db.Taxes.Include(t => t.Rates)
            .Where(t => taxIds.Contains(t.Id))
            .ToListAsync(ct);

        var productById = products.ToDictionary(p => p.Id);
        var taxById = taxes.ToDictionary(t => t.Id);

        var sequence = await _db.Invoices.IgnoreQueryFilters().CountAsync(ct) + 1;
        var invoice = new Invoice
        {
            Number = DocumentNumber.For("INV", sequence),
            CustomerId = order.CustomerId,
            SalesOrderId = order.Id,
            IssueDate = request.IssueDate,
            DueDate = request.DueDate
        };

        foreach (var line in order.Lines)
        {
            var product = productById[line.ProductId];
            var rate = taxById.TryGetValue(product.TaxId, out var tax)
                ? tax.GetRateOn(request.IssueDate)
                : 0m;
            invoice.AddLine(line.ProductId, product.Name, line.Quantity, line.UnitPrice, rate);
        }

        _db.Invoices.Add(invoice);
        await _db.SaveChangesAsync(ct);
        return Result.Success(invoice.Id);
    }
}
