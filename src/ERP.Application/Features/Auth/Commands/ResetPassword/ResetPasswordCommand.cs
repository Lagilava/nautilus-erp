using ERP.Shared.Results;
using MediatR;

namespace ERP.Application.Features.Auth.Commands.ResetPassword;

/// <summary>Completes a password reset using the token from <c>ForgotPassword</c>.</summary>
public sealed record ResetPasswordCommand(string Email, string Token, string NewPassword)
    : IRequest<Result>;
