using ERP.Application.Common.Interfaces;
using ERP.Shared.Results;
using MediatR;

namespace ERP.Application.Features.Auth.Commands.ResetPassword;

public sealed class ResetPasswordCommandHandler : IRequestHandler<ResetPasswordCommand, Result>
{
    private readonly IIdentityService _identity;

    public ResetPasswordCommandHandler(IIdentityService identity) => _identity = identity;

    public Task<Result> Handle(ResetPasswordCommand request, CancellationToken cancellationToken)
        => _identity.ResetPasswordAsync(request.Email, request.Token, request.NewPassword, cancellationToken);
}
