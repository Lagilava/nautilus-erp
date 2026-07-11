using ERP.Application.Common.Models;
using ERP.Shared.Results;
using MediatR;

namespace ERP.Application.Features.Auth.Commands.Login;

/// <summary>
/// Authenticates by email + password. Returns a token pair directly, or — when the account
/// has MFA enabled — an <see cref="LoginResult.MfaChallengeToken"/> that must be redeemed via
/// <c>VerifyMfaCommand</c> before tokens are issued.
/// </summary>
public sealed record LoginCommand(string Email, string Password)
    : IRequest<Result<LoginResult>>;
