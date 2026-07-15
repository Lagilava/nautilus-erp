using ERP.API.Common;
using ERP.Application.Features.Accounting.Periods;
using ERP.Shared.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERP.API.Controllers;

/// <summary>
/// Accounting period locking. Listing is open to any authenticated user; closing a period
/// (which blocks any further posting into it) is Administrator-only.
/// </summary>
[Authorize]
[Route("api/accounting-periods")]
public sealed class AccountingPeriodsController : ApiControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct)
        => HandleResult(await Sender.Send(new GetAccountingPeriodsQuery(), ct));

    [HttpPost("close")]
    [Authorize(Roles = Roles.Administrator)]
    public async Task<IActionResult> Close(ClosePeriodCommand command, CancellationToken ct)
        => HandleResult(await Sender.Send(command, ct));
}
