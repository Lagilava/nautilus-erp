using ERP.Application.Common.Interfaces;
using ERP.Application.Common.Security;
using ERP.Application.Features.Inventory;
using ERP.Application.Features.Sales;
using ERP.Domain.Common;
using ERP.Domain.Inventory;
using ERP.Domain.Purchasing;
using ERP.Shared.Results;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ERP.Application.Features.Purchasing.GoodsReceipts;

public sealed record GoodsReceiptLineInput(Guid PurchaseOrderLineId, decimal Quantity);

/// <summary>
/// Posts a goods receipt against a purchase order. The mirror of sales fulfilment: it
/// <b>receives FIFO stock</b> into the order's warehouse (a new cost layer per line at the
/// PO unit cost) and advances the PO to PartiallyReceived/Received. All-or-nothing —
/// over-receiving any line rejects the whole receipt.
/// </summary>
public sealed record PostGoodsReceiptCommand(
    Guid PurchaseOrderId,
    DateOnly ReceivedDate,
    IReadOnlyList<GoodsReceiptLineInput> Lines,
    string? Notes) : IRequest<Result<Guid>>;

public sealed class PostGoodsReceiptCommandValidator : AbstractValidator<PostGoodsReceiptCommand>
{
    public PostGoodsReceiptCommandValidator()
    {
        RuleFor(x => x.PurchaseOrderId).NotEmpty();
        RuleFor(x => x.Lines).NotEmpty();
        RuleForEach(x => x.Lines).ChildRules(line =>
        {
            line.RuleFor(l => l.PurchaseOrderLineId).NotEmpty();
            line.RuleFor(l => l.Quantity).GreaterThan(0);
        });
    }
}

public sealed class PostGoodsReceiptCommandHandler : IRequestHandler<PostGoodsReceiptCommand, Result<Guid>>
{
    private readonly IApplicationDbContext _db;
    private readonly IDateTime _clock;
    private readonly ISegregationOfDuties _sod;
    private readonly IBranchScope _scope;

    public PostGoodsReceiptCommandHandler(
        IApplicationDbContext db, IDateTime clock, ISegregationOfDuties sod, IBranchScope scope)
    {
        _db = db;
        _clock = clock;
        _sod = sod;
        _scope = scope;
    }

    public async Task<Result<Guid>> Handle(PostGoodsReceiptCommand request, CancellationToken ct)
    {
        var order = await _db.PurchaseOrders.Include(o => o.Lines)
            .FirstOrDefaultAsync(o => o.Id == request.PurchaseOrderId, ct);
        if (order is null)
            return Result.Failure<Guid>(Error.NotFound("Purchase order not found."));

        // The receipt lands stock in the order's warehouse, so the receiver must be entitled to
        // it. NotFound rather than Unauthorized — a 403 would confirm the order exists.
        if (!await _scope.CanAccessWarehouseAsync(order.WarehouseId, ct))
            return Result.Failure<Guid>(Error.NotFound("Purchase order not found."));

        // Purchasing must be separate from receiving: whoever raised or approved the order
        // must not be the one confirming the goods arrived.
        var sod = _sod.Ensure(SoDRule.GoodsReceipt,
            "You cannot receive goods against a purchase order you raised or approved.",
            order.CreatedBy, order.ConfirmedBy);
        if (sod.IsFailure) return Result.Failure<Guid>(sod.Error);

        var sequence = await _db.GoodsReceipts.IgnoreQueryFilters().CountAsync(ct) + 1;
        var receipt = new GoodsReceipt
        {
            Number = DocumentNumber.For("GRN", sequence),
            PurchaseOrderId = order.Id,
            WarehouseId = order.WarehouseId,
            ReceivedDate = request.ReceivedDate,
            Notes = request.Notes
        };

        var now = _clock.UtcNow;

        foreach (var input in request.Lines)
        {
            PurchaseOrderLine poLine;
            try { poLine = order.ReceiveLine(input.PurchaseOrderLineId, input.Quantity); }
            catch (DomainException ex) { return Result.Failure<Guid>(Error.Conflict(ex.Message)); }

            var item = await InventoryItemLoader.GetOrCreateAsync(_db, poLine.ProductId, order.WarehouseId, ct);
            var totalCost = item.Receive(input.Quantity, poLine.UnitCost);

            receipt.Lines.Add(new GoodsReceiptLine
            {
                GoodsReceiptId = receipt.Id,
                PurchaseOrderLineId = poLine.Id,
                ProductId = poLine.ProductId,
                Quantity = input.Quantity,
                UnitCost = poLine.UnitCost
            });

            _db.StockMovements.Add(new StockMovement
            {
                ProductId = poLine.ProductId,
                WarehouseId = order.WarehouseId,
                Type = MovementType.Receipt,
                Quantity = input.Quantity,
                UnitCost = poLine.UnitCost,
                TotalCost = totalCost,
                OccurredAt = now,
                Reference = order.Number,
                Notes = "Goods receipt"
            });
        }

        _db.GoodsReceipts.Add(receipt);
        await _db.SaveChangesAsync(ct);
        return Result.Success(receipt.Id);
    }
}
