using ERP.API.Common;
using ERP.Application.Features.Admin;
using ERP.Shared.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERP.API.Controllers;

/// <summary>User &amp; role administration. Administrator-only.</summary>
[Authorize(Roles = Roles.Administrator)]
[Route("api/users")]
public sealed class UsersController : ApiControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct)
        => HandleResult(await Sender.Send(new GetUsersQuery(), ct));

    [HttpGet("/api/roles")]
    public async Task<IActionResult> GetRoles(CancellationToken ct)
        => HandleResult(await Sender.Send(new GetRolesQuery(), ct));

    [HttpPost]
    public async Task<IActionResult> Create(CreateUserCommand command, CancellationToken ct)
        => HandleResult(await Sender.Send(command, ct));

    [HttpPut("{id:guid}/roles")]
    public async Task<IActionResult> SetRoles(Guid id, SetUserRolesCommand command, CancellationToken ct)
        => id != command.UserId
            ? BadRequest("Route id and body userId must match.")
            : HandleResult(await Sender.Send(command, ct));

    [HttpPost("{id:guid}/active")]
    public async Task<IActionResult> SetActive(Guid id, [FromBody] bool isActive, CancellationToken ct)
        => HandleResult(await Sender.Send(new SetUserActiveCommand(id, isActive), ct));
}
