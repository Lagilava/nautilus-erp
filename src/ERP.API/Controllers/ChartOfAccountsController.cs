using ERP.API.Common;
using ERP.Application.Features.Accounting.ChartOfAccounts;
using ERP.Shared.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERP.API.Controllers;

/// <summary>
/// Chart of accounts CRUD. GET routes: `/api/chart-of-accounts`, `/api/chart-of-accounts/{id}`.
/// Reads: any authenticated user. Creating/deactivating an account is Administrator-only —
/// the chart of accounts is the structural skeleton every posting relies on.
/// </summary>
[Authorize]
[Route("api/chart-of-accounts")]
public sealed class ChartOfAccountsController : ApiControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] bool? activeOnly, CancellationToken ct)
        => HandleResult(await Sender.Send(new GetAccountsQuery(activeOnly), ct));

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
        => HandleResult(await Sender.Send(new GetAccountByIdQuery(id), ct));

    [HttpPost]
    [Authorize(Roles = Roles.Administrator)]
    public async Task<IActionResult> Create(CreateAccountCommand command, CancellationToken ct)
        => HandleResult(await Sender.Send(command, ct));

    [HttpPost("{id:guid}/deactivate")]
    [Authorize(Roles = Roles.Administrator)]
    public async Task<IActionResult> Deactivate(Guid id, CancellationToken ct)
        => HandleResult(await Sender.Send(new DeactivateAccountCommand(id), ct));
}
