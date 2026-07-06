using ERP.Application.Common.Interfaces;
using ERP.Shared.Results;
using MediatR;

namespace ERP.Application.Features.Auth.Commands.ForgotPassword;

public sealed class ForgotPasswordCommandHandler
    : IRequestHandler<ForgotPasswordCommand, Result<string?>>
{
    private readonly IIdentityService _identity;

    public ForgotPasswordCommandHandler(IIdentityService identity) => _identity = identity;

    public async Task<Result<string?>> Handle(
        ForgotPasswordCommand request, CancellationToken cancellationToken)
    {
        var tokenResult = await _identity.GeneratePasswordResetTokenAsync(request.Email, cancellationToken);

        // Deliberately do not reveal whether the account exists: return success either way.
        // A real email dispatch (Milestone 9) replaces returning the token in-band.
        return Result.Success(tokenResult.IsSuccess ? tokenResult.Value : null);
    }
}
