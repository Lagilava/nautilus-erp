using ERP.API.Common;
using ERP.Application.Features.Admin;
using ERP.Shared.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERP.API.Controllers;

/// <summary>The business's own identity (used on tax invoices). Read: any user; write: Administrator.</summary>
[Authorize]
[Route("api/company")]
public sealed class CompanyController : ApiControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken ct)
        => HandleResult(await Sender.Send(new GetCompanyProfileQuery(), ct));

    [HttpPut]
    [Authorize(Roles = Roles.Administrator)]
    public async Task<IActionResult> Update(UpdateCompanyProfileCommand command, CancellationToken ct)
        => HandleResult(await Sender.Send(command, ct));
}
