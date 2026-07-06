using ERP.Shared.Results;
using MediatR;

namespace ERP.Application.Features.Auth.Commands.ForgotPassword;

/// <summary>
/// Initiates a password reset. Always succeeds regardless of whether the email exists
/// (no account enumeration). The reset token is returned only for a known account and,
/// in production, delivered out-of-band via email rather than in the response.
/// </summary>
public sealed record ForgotPasswordCommand(string Email) : IRequest<Result<string?>>;
