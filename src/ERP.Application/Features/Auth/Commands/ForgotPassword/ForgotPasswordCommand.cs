using ERP.Shared.Results;
using MediatR;

namespace ERP.Application.Features.Auth.Commands.ForgotPassword;

/// <summary>
/// Initiates a password reset. The reset token is delivered out-of-band by email and is
/// NEVER returned to the caller. The response is identical whether or not the account
/// exists, so this cannot be used to enumerate accounts.
/// </summary>
public sealed record ForgotPasswordCommand(string Email) : IRequest<Result>;
