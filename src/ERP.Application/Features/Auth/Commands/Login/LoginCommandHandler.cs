using ERP.Application.Common.Interfaces;
using ERP.Application.Common.Models;
using ERP.Domain.Identity;
using ERP.Shared.Results;
using MediatR;

namespace ERP.Application.Features.Auth.Commands.Login;

public sealed class LoginCommandHandler
    : IRequestHandler<LoginCommand, Result<AuthenticationResult>>
{
    private readonly IIdentityService _identity;
    private readonly IAuthTokenIssuer _tokenIssuer;
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTime _clock;

    public LoginCommandHandler(
        IIdentityService identity, IAuthTokenIssuer tokenIssuer, IApplicationDbContext db,
        ICurrentUserService currentUser, IDateTime clock)
    {
        _identity = identity;
        _tokenIssuer = tokenIssuer;
        _db = db;
        _currentUser = currentUser;
        _clock = clock;
    }

    public async Task<Result<AuthenticationResult>> Handle(
        LoginCommand request, CancellationToken cancellationToken)
    {
        var validation = await _identity.ValidateCredentialsAsync(
            request.Email, request.Password, cancellationToken);

        if (validation.IsFailure)
        {
            await RecordAttemptAsync(
                userId: null, request.Email, succeeded: false,
                failureReason: validation.Error.Code, cancellationToken);
            return Result.Failure<AuthenticationResult>(validation.Error);
        }

        var user = validation.Value;
        await RecordAttemptAsync(user.Id, request.Email, succeeded: true, null, cancellationToken);

        var auth = await _tokenIssuer.IssueAsync(user, _currentUser.IpAddress, cancellationToken);
        return Result.Success(auth);
    }

    private async Task RecordAttemptAsync(
        Guid? userId, string email, bool succeeded, string? failureReason, CancellationToken ct)
    {
        _db.LoginHistories.Add(new LoginHistory
        {
            UserId = userId,
            AttemptedEmail = email,
            Succeeded = succeeded,
            FailureReason = failureReason,
            IpAddress = _currentUser.IpAddress,
            UserAgent = _currentUser.UserAgent,
            OccurredAt = _clock.UtcNow
        });
        await _db.SaveChangesAsync(ct);
    }
}
