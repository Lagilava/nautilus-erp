using ERP.API.Common;
using ERP.Application.Features.Sales.SalesOrders;
using ERP.Shared.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERP.API.Controllers;

/// <summary>Sales orders and their lifecycle. Writes: Manager/Administrator.</summary>
[Authorize]
[Route("api/sales-orders")]
public sealed class SalesOrdersController : ApiControllerBase
{
    private const string Writers = $"{Roles.Administrator},{Roles.Manager}";

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] GetSalesOrdersQuery query, CancellationToken ct)
        => HandleResult(await Sender.Send(query, ct));

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
        => HandleResult(await Sender.Send(new GetSalesOrderByIdQuery(id), ct));

    [HttpPost]
    [Authorize(Roles = Writers)]
    public async Task<IActionResult> Create(CreateSalesOrderCommand command, CancellationToken ct)
        => HandleResult(await Sender.Send(command, ct));

    [HttpPost("{id:guid}/confirm")]
    [Authorize(Roles = Writers)]
    public async Task<IActionResult> Confirm(Guid id, CancellationToken ct)
        => HandleResult(await Sender.Send(new ConfirmSalesOrderCommand(id), ct));

    [HttpPost("{id:guid}/fulfill")]
    [Authorize(Roles = Writers)]
    public async Task<IActionResult> Fulfill(Guid id, CancellationToken ct)
        => HandleResult(await Sender.Send(new FulfillSalesOrderCommand(id), ct));

    [HttpPost("{id:guid}/cancel")]
    [Authorize(Roles = Writers)]
    public async Task<IActionResult> Cancel(Guid id, CancellationToken ct)
        => HandleResult(await Sender.Send(new CancelSalesOrderCommand(id), ct));
}
