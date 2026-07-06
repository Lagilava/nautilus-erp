using ERP.API.Common;
using ERP.Application.Features.Purchasing.GoodsReceipts;
using ERP.Application.Features.Purchasing.PurchaseOrders;
using ERP.Shared.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERP.API.Controllers;

/// <summary>Purchase orders, their lifecycle, and goods receipts. Writes: Manager/Administrator.</summary>
[Authorize]
[Route("api/purchase-orders")]
public sealed class PurchaseOrdersController : ApiControllerBase
{
    private const string Writers = $"{Roles.Administrator},{Roles.Manager}";

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] GetPurchaseOrdersQuery query, CancellationToken ct)
        => HandleResult(await Sender.Send(query, ct));

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
        => HandleResult(await Sender.Send(new GetPurchaseOrderByIdQuery(id), ct));

    [HttpPost]
    [Authorize(Roles = Writers)]
    public async Task<IActionResult> Create(CreatePurchaseOrderCommand command, CancellationToken ct)
        => HandleResult(await Sender.Send(command, ct));

    [HttpPost("{id:guid}/confirm")]
    [Authorize(Roles = Writers)]
    public async Task<IActionResult> Confirm(Guid id, CancellationToken ct)
        => HandleResult(await Sender.Send(new ConfirmPurchaseOrderCommand(id), ct));

    [HttpPost("{id:guid}/cancel")]
    [Authorize(Roles = Writers)]
    public async Task<IActionResult> Cancel(Guid id, CancellationToken ct)
        => HandleResult(await Sender.Send(new CancelPurchaseOrderCommand(id), ct));

    [HttpPost("{id:guid}/receipts")]
    [Authorize(Roles = Writers)]
    public async Task<IActionResult> Receive(Guid id, PostGoodsReceiptCommand command, CancellationToken ct)
        => id != command.PurchaseOrderId
            ? BadRequest("Route id and body purchaseOrderId must match.")
            : HandleResult(await Sender.Send(command, ct));
}
