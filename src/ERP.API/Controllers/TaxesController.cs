using ERP.API.Common;
using ERP.Application.Features.Taxation.Taxes.Commands;
using ERP.Application.Features.Taxation.Taxes.Queries;
using ERP.Shared.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERP.API.Controllers;

/// <summary>Tax definitions and their effective-dated rates. Writes: Manager/Administrator.</summary>
[Authorize]
public sealed class TaxesController : ApiControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct)
        => HandleResult(await Sender.Send(new GetTaxesQuery(), ct));

    [HttpPost]
    [Authorize(Roles = $"{Roles.Administrator},{Roles.Manager}")]
    public async Task<IActionResult> Create(CreateTaxCommand command, CancellationToken ct)
        => HandleResult(await Sender.Send(command, ct));

    [HttpPost("{id:guid}/rates")]
    [Authorize(Roles = $"{Roles.Administrator},{Roles.Manager}")]
    public async Task<IActionResult> AddRate(Guid id, AddTaxRateCommand command, CancellationToken ct)
        => id != command.TaxId
            ? BadRequest("Route id and body taxId must match.")
            : HandleResult(await Sender.Send(command, ct));
}
