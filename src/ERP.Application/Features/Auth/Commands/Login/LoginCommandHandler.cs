using ERP.Application.Common.Interfaces;
using ERP.Application.Common.Models;
using ERP.Domain.Identity;
using ERP.Shared.Results;
using MediatR;

namespace ERP.Application.Features.Auth.Commands.Login;

public sealed class LoginCommandHandler
    : IRequestHandler<LoginCommand, Result<LoginResult>>
{
    private readonly IIdentityService _identity;
    private readonly IAuthTokenIssuer _tokenIssuer;
    private readonly ITokenService _tokens;
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTime _clock;

    public LoginCommandHandler(
        IIdentityService identity, IAuthTokenIssuer tokenIssuer, ITokenService tokens, IApplicationDbContext db,
        ICurrentUserService currentUser, IDateTime clock)
    {
        _identity = identity;
        _tokenIssuer = tokenIssuer;
        _tokens = tokens;
        _db = db;
        _currentUser = currentUser;
        _clock = clock;
    }

    public async Task<Result<LoginResult>> Handle(
        LoginCommand request, CancellationToken cancellationToken)
    {
        var validation = await _identity.ValidateCredentialsAsync(
            request.Email, request.Password, cancellationToken);

        if (validation.IsFailure)
        {
            await RecordAttemptAsync(
                userId: null, request.Email, succeeded: false,
                failureReason: validation.Error.Code, cancellationToken);
            return Result.Failure<LoginResult>(validation.Error);
        }

        var user = validation.Value;

        if (user.MfaEnabled)
        {
            // Password verified, but the second factor is still owed. Record it as a
            // successful attempt (the credential was correct) and hand back a challenge
            // token rather than tokens — VerifyMfaCommand issues real tokens once the
            // code is redeemed.
            await RecordAttemptAsync(user.Id, request.Email, succeeded: true, null, cancellationToken);
            var challenge = _tokens.CreateMfaChallengeToken(user.Id);
            return Result.Success(new LoginResult(true, challenge, null));
        }

        await RecordAttemptAsync(user.Id, request.Email, succeeded: true, null, cancellationToken);

        var auth = await _tokenIssuer.IssueAsync(user, _currentUser.IpAddress, cancellationToken);
        return Result.Success(new LoginResult(false, null, auth));
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
