using ERP.Application.Common.Interfaces;
using ERP.Application.Common.Models;
using ERP.Shared.Results;
using MediatR;

namespace ERP.Application.Features.Auth.Queries.GetCurrentUser;

public sealed class GetCurrentUserQueryHandler
    : IRequestHandler<GetCurrentUserQuery, Result<UserIdentity>>
{
    private readonly ICurrentUserService _currentUser;
    private readonly IIdentityService _identity;

    public GetCurrentUserQueryHandler(ICurrentUserService currentUser, IIdentityService identity)
    {
        _currentUser = currentUser;
        _identity = identity;
    }

    public async Task<Result<UserIdentity>> Handle(
        GetCurrentUserQuery request, CancellationToken cancellationToken)
    {
        if (_currentUser.UserId is not { } userId)
            return Result.Failure<UserIdentity>(Error.Unauthorized("Not authenticated."));

        return await _identity.GetByIdAsync(userId, cancellationToken);
    }
}
