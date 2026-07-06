using ERP.API.Common;
using ERP.Application.Features.Organization.Warehouses;
using ERP.Shared.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERP.API.Controllers;

[Authorize]
public sealed class WarehousesController : ApiControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct)
        => HandleResult(await Sender.Send(new GetWarehousesQuery(), ct));

    [HttpPost]
    [Authorize(Roles = $"{Roles.Administrator},{Roles.Manager}")]
    public async Task<IActionResult> Create(CreateWarehouseCommand command, CancellationToken ct)
        => HandleResult(await Sender.Send(command, ct));
}
