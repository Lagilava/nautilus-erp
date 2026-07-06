using ERP.API.Common;
using ERP.Application.Features.Dashboard;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERP.API.Controllers;

/// <summary>Home dashboard KPIs for any authenticated user.</summary>
[Authorize]
public sealed class DashboardController : ApiControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken ct)
        => HandleResult(await Sender.Send(new GetDashboardQuery(), ct));
}
