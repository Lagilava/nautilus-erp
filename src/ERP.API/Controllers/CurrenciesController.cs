using ERP.API.Common;
using ERP.Application.Features.Catalog.Currencies;
using ERP.Shared.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERP.API.Controllers;

[Authorize]
public sealed class CurrenciesController : ApiControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct)
        => HandleResult(await Sender.Send(new GetCurrenciesQuery(), ct));

    [HttpPost]
    [Authorize(Roles = $"{Roles.Administrator},{Roles.Manager}")]
    public async Task<IActionResult> Create(CreateCurrencyCommand command, CancellationToken ct)
        => HandleResult(await Sender.Send(command, ct));
}
