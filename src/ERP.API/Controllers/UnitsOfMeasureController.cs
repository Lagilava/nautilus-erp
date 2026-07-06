using ERP.API.Common;
using ERP.Application.Features.Catalog.UnitsOfMeasure;
using ERP.Shared.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERP.API.Controllers;

[Authorize]
[Route("api/units-of-measure")]
public sealed class UnitsOfMeasureController : ApiControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct)
        => HandleResult(await Sender.Send(new GetUnitsOfMeasureQuery(), ct));

    [HttpPost]
    [Authorize(Roles = $"{Roles.Administrator},{Roles.Manager}")]
    public async Task<IActionResult> Create(CreateUnitOfMeasureCommand command, CancellationToken ct)
        => HandleResult(await Sender.Send(command, ct));
}
