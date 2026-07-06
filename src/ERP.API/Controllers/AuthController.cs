using ERP.API.Common;
using ERP.Application.Features.Auth.Commands.ForgotPassword;
using ERP.Application.Features.Auth.Commands.Login;
using ERP.Application.Features.Auth.Commands.Logout;
using ERP.Application.Features.Auth.Commands.Refresh;
using ERP.Application.Features.Auth.Commands.Register;
using ERP.Application.Features.Auth.Commands.ResetPassword;
using ERP.Application.Features.Auth.Queries.GetCurrentUser;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERP.API.Controllers;

/// <summary>Authentication endpoints. Thin: each action forwards to a MediatR request.</summary>
public sealed class AuthController : ApiControllerBase
{
    [HttpPost("register")]
    [AllowAnonymous]
    public async Task<IActionResult> Register(RegisterCommand command, CancellationToken ct)
        => HandleResult(await Sender.Send(command, ct));

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<IActionResult> Login(LoginCommand command, CancellationToken ct)
        => HandleResult(await Sender.Send(command, ct));

    [HttpPost("refresh")]
    [AllowAnonymous]
    public async Task<IActionResult> Refresh(RefreshTokenCommand command, CancellationToken ct)
        => HandleResult(await Sender.Send(command, ct));

    [HttpPost("logout")]
    [Authorize]
    public async Task<IActionResult> Logout(LogoutCommand command, CancellationToken ct)
        => HandleResult(await Sender.Send(command, ct));

    [HttpPost("forgot-password")]
    [AllowAnonymous]
    public async Task<IActionResult> ForgotPassword(ForgotPasswordCommand command, CancellationToken ct)
        => HandleResult(await Sender.Send(command, ct));

    [HttpPost("reset-password")]
    [AllowAnonymous]
    public async Task<IActionResult> ResetPassword(ResetPasswordCommand command, CancellationToken ct)
        => HandleResult(await Sender.Send(command, ct));

    [HttpGet("me")]
    [Authorize]
    public async Task<IActionResult> Me(CancellationToken ct)
        => HandleResult(await Sender.Send(new GetCurrentUserQuery(), ct));
}
