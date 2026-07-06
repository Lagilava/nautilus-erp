using ERP.API.Common;
using ERP.Application.Features.Inventory.Commands;
using ERP.Application.Features.Inventory.Queries;
using ERP.Shared.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERP.API.Controllers;

/// <summary>
/// Inventory operations over the stock ledger. Reads: any authenticated user.
/// Stock-changing operations: Manager/Administrator.
/// </summary>
[Authorize]
[Route("api/inventory")]
public sealed class InventoryController : ApiControllerBase
{
    private const string Writers = $"{Roles.Administrator},{Roles.Manager}";

    [HttpGet("levels")]
    public async Task<IActionResult> Levels([FromQuery] GetStockLevelsQuery query, CancellationToken ct)
        => HandleResult(await Sender.Send(query, ct));

    [HttpGet("movements")]
    public async Task<IActionResult> Movements([FromQuery] GetStockMovementsQuery query, CancellationToken ct)
        => HandleResult(await Sender.Send(query, ct));

    [HttpPost("receive")]
    [Authorize(Roles = Writers)]
    public async Task<IActionResult> Receive(ReceiveStockCommand command, CancellationToken ct)
        => HandleResult(await Sender.Send(command, ct));

    [HttpPost("issue")]
    [Authorize(Roles = Writers)]
    public async Task<IActionResult> Issue(IssueStockCommand command, CancellationToken ct)
        => HandleResult(await Sender.Send(command, ct));

    [HttpPost("transfer")]
    [Authorize(Roles = Writers)]
    public async Task<IActionResult> Transfer(TransferStockCommand command, CancellationToken ct)
        => HandleResult(await Sender.Send(command, ct));

    [HttpPost("adjust")]
    [Authorize(Roles = Writers)]
    public async Task<IActionResult> Adjust(AdjustStockCommand command, CancellationToken ct)
        => HandleResult(await Sender.Send(command, ct));

    [HttpPut("reorder-level")]
    [Authorize(Roles = Writers)]
    public async Task<IActionResult> SetReorderLevel(SetReorderLevelCommand command, CancellationToken ct)
        => HandleResult(await Sender.Send(command, ct));
}
