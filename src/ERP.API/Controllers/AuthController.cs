using ERP.API.Common;
using ERP.Application.Features.Auth.Commands.ForgotPassword;
using ERP.Application.Features.Auth.Commands.Login;
using ERP.Application.Features.Auth.Commands.Logout;
using ERP.Application.Features.Auth.Commands.Mfa;
using ERP.Application.Features.Auth.Commands.Profile;
using ERP.Application.Features.Auth.Commands.Refresh;
using ERP.Application.Features.Auth.Commands.ResetPassword;
using ERP.Application.Features.Auth.Commands.VerifyMfa;
using ERP.Application.Features.Auth.Queries.GetCurrentUser;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERP.API.Controllers;

/// <summary>
/// Authentication and self-service account endpoints. Thin: each action forwards to a
/// MediatR request.
///
/// There is deliberately no public self-registration: an ERP tenant's users are created by
/// an administrator (<c>POST /api/users</c>). Anonymous sign-up would grant a stranger
/// read access to the whole business.
/// </summary>
public sealed class AuthController : ApiControllerBase
{
    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<IActionResult> Login(LoginCommand command, CancellationToken ct)
        => HandleResult(await Sender.Send(command, ct));

    /// <summary>Redeems the challenge from a login whose account has MFA enabled.</summary>
    [HttpPost("mfa/verify")]
    [AllowAnonymous]
    public async Task<IActionResult> VerifyMfa(VerifyMfaCommand command, CancellationToken ct)
        => HandleResult(await Sender.Send(command, ct));

    [HttpPost("refresh")]
    [AllowAnonymous]
    public async Task<IActionResult> Refresh(RefreshTokenCommand command, CancellationToken ct)
        => HandleResult(await Sender.Send(command, ct));

    [HttpPost("logout")]
    [Authorize]
    public async Task<IActionResult> Logout(LogoutCommand command, CancellationToken ct)
        => HandleResult(await Sender.Send(command, ct));

    /// <summary>Emails a reset link. Always 204, whether or not the account exists.</summary>
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

    /// <summary>Self-service: update your own display name.</summary>
    [HttpPut("me")]
    [Authorize]
    public async Task<IActionResult> UpdateProfile(UpdateProfileCommand command, CancellationToken ct)
        => HandleResult(await Sender.Send(command, ct));

    /// <summary>Self-service: change your own password. Requires the current password.</summary>
    [HttpPost("change-password")]
    [Authorize]
    public async Task<IActionResult> ChangePassword(ChangePasswordCommand command, CancellationToken ct)
        => HandleResult(await Sender.Send(command, ct));

    /// <summary>Self-service: (re)generate an authenticator secret. Not yet active — see mfa/enable.</summary>
    [HttpPost("mfa/setup")]
    [Authorize]
    public async Task<IActionResult> BeginMfaSetup(CancellationToken ct)
        => HandleResult(await Sender.Send(new BeginMfaSetupCommand(), ct));

    /// <summary>Self-service: confirm setup with a code and turn MFA on. Returns recovery codes once.</summary>
    [HttpPost("mfa/enable")]
    [Authorize]
    public async Task<IActionResult> EnableMfa(EnableMfaCommand command, CancellationToken ct)
        => HandleResult(await Sender.Send(command, ct));

    /// <summary>Self-service: turn MFA off. Requires the current password.</summary>
    [HttpPost("mfa/disable")]
    [Authorize]
    public async Task<IActionResult> DisableMfa(DisableMfaCommand command, CancellationToken ct)
        => HandleResult(await Sender.Send(command, ct));
}
