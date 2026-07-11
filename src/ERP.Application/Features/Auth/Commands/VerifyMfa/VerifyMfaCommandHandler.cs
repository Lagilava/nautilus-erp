using ERP.Application.Common.Interfaces;
using ERP.Application.Common.Models;
using ERP.Domain.Identity;
using ERP.Shared.Results;
using MediatR;

namespace ERP.Application.Features.Auth.Commands.VerifyMfa;

public sealed class VerifyMfaCommandHandler : IRequestHandler<VerifyMfaCommand, Result<AuthenticationResult>>
{
    private readonly ITokenService _tokens;
    private readonly IIdentityService _identity;
    private readonly IAuthTokenIssuer _tokenIssuer;
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTime _clock;

    public VerifyMfaCommandHandler(
        ITokenService tokens, IIdentityService identity, IAuthTokenIssuer tokenIssuer, IApplicationDbContext db,
        ICurrentUserService currentUser, IDateTime clock)
    {
        _tokens = tokens;
        _identity = identity;
        _tokenIssuer = tokenIssuer;
        _db = db;
        _currentUser = currentUser;
        _clock = clock;
    }

    public async Task<Result<AuthenticationResult>> Handle(VerifyMfaCommand request, CancellationToken ct)
    {
        var challenge = _tokens.ValidateMfaChallengeToken(request.ChallengeToken);
        if (challenge.IsFailure)
            return Result.Failure<AuthenticationResult>(challenge.Error);

        var userId = challenge.Value;
        var verified = await _identity.VerifyMfaCodeAsync(userId, request.Code, ct);

        // The user is known (the challenge token named them), so this attempt can always be
        // attributed even on failure — unlike the password step, which must stay silent about
        // whether the email exists.
        var email = (await _identity.GetByIdAsync(userId, ct)) is { IsSuccess: true } lookup
            ? lookup.Value.Email
            : "unknown";

        if (verified.IsFailure)
        {
            await RecordAttemptAsync(userId, email, succeeded: false, verified.Error.Code, ct);
            return Result.Failure<AuthenticationResult>(verified.Error);
        }

        await RecordAttemptAsync(userId, email, succeeded: true, null, ct);
        var auth = await _tokenIssuer.IssueAsync(verified.Value, _currentUser.IpAddress, ct);
        return Result.Success(auth);
    }

    private async Task RecordAttemptAsync(
        Guid userId, string email, bool succeeded, string? failureReason, CancellationToken ct)
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
