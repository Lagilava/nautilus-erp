using FluentValidation;

namespace ERP.Application.Features.Auth.Commands.Refresh;

public sealed class RefreshTokenCommandValidator : AbstractValidator<RefreshTokenCommand>
{
    public RefreshTokenCommandValidator()
        => RuleFor(x => x.RefreshToken).NotEmpty();
}
