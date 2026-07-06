using ERP.API.Common;
using ERP.Application.Features.Purchasing.Suppliers;
using ERP.Shared.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERP.API.Controllers;

[Authorize]
public sealed class SuppliersController : ApiControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] GetSuppliersQuery query, CancellationToken ct)
        => HandleResult(await Sender.Send(query, ct));

    [HttpPost]
    [Authorize(Roles = $"{Roles.Administrator},{Roles.Manager}")]
    public async Task<IActionResult> Create(CreateSupplierCommand command, CancellationToken ct)
        => HandleResult(await Sender.Send(command, ct));
}
