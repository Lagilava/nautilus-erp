using ERP.API.Common;
using ERP.Application.Features.Auditing;
using ERP.Shared.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERP.API.Controllers;

/// <summary>The audit trail. Administrator-only — it exposes who changed what.</summary>
[Authorize(Roles = Roles.Administrator)]
[Route("api/audit-logs")]
public sealed class AuditLogsController : ApiControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] GetAuditLogsQuery query, CancellationToken ct)
        => HandleResult(await Sender.Send(query, ct));
}
