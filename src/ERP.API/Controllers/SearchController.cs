using ERP.API.Common;
using ERP.Application.Features.Search;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERP.API.Controllers;

/// <summary>Global search across the major entities — backs the Ctrl+K command palette.</summary>
[Authorize]
[Route("api/search")]
public sealed class SearchController : ApiControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Search([FromQuery] string q, CancellationToken ct)
        => HandleResult(await Sender.Send(new GlobalSearchQuery(q), ct));
}
