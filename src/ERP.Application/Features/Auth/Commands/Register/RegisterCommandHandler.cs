using ERP.Application.Common.Interfaces;
using ERP.Application.Common.Models;
using ERP.Shared.Authorization;
using ERP.Shared.Results;
using MediatR;

namespace ERP.Application.Features.Auth.Commands.Register;

public sealed class RegisterCommandHandler
    : IRequestHandler<RegisterCommand, Result<AuthenticationResult>>
{
    private readonly IIdentityService _identity;
    private readonly IAuthTokenIssuer _tokenIssuer;
    private readonly ICurrentUserService _currentUser;

    public RegisterCommandHandler(
        IIdentityService identity, IAuthTokenIssuer tokenIssuer, ICurrentUserService currentUser)
    {
        _identity = identity;
        _tokenIssuer = tokenIssuer;
        _currentUser = currentUser;
    }

    public async Task<Result<AuthenticationResult>> Handle(
        RegisterCommand request, CancellationToken cancellationToken)
    {
        // New self-registered users get the least-privileged role by default.
        await _identity.EnsureRoleAsync(Roles.Staff, cancellationToken);

        var created = await _identity.CreateUserAsync(
            request.Email, request.Password, request.FirstName, request.LastName,
            new[] { Roles.Staff }, cancellationToken);

        if (created.IsFailure)
            return Result.Failure<AuthenticationResult>(created.Error);

        var auth = await _tokenIssuer.IssueAsync(
            created.Value, _currentUser.IpAddress, cancellationToken);

        return Result.Success(auth);
    }
}
