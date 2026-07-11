using ERP.Application.Common.Models;
using ERP.Shared.Results;
using FluentValidation;
using MediatR;

namespace ERP.Application.Features.Auth.Commands.VerifyMfa;

/// <summary>Redeems an MFA challenge (from <c>LoginCommand</c>) with a TOTP or recovery code.</summary>
public sealed record VerifyMfaCommand(string ChallengeToken, string Code) : IRequest<Result<AuthenticationResult>>;

public sealed class VerifyMfaCommandValidator : AbstractValidator<VerifyMfaCommand>
{
    public VerifyMfaCommandValidator()
    {
        RuleFor(x => x.ChallengeToken).NotEmpty();
        RuleFor(x => x.Code).NotEmpty().MaximumLength(50);
    }
}
