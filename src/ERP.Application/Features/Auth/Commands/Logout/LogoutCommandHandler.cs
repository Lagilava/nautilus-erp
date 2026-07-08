using ERP.Application.Common.Interfaces;
using ERP.Shared.Results;
using ERP.Shared.Security;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ERP.Application.Features.Auth.Commands.Logout;

public sealed class LogoutCommandHandler : IRequestHandler<LogoutCommand, Result>
{
    private readonly IApplicationDbContext _db;
    private readonly IDateTime _clock;

    public LogoutCommandHandler(IApplicationDbContext db, IDateTime clock)
    {
        _db = db;
        _clock = clock;
    }

    public async Task<Result> Handle(LogoutCommand request, CancellationToken cancellationToken)
    {
        var now = _clock.UtcNow;
        var presentedHash = TokenHasher.Hash(request.RefreshToken);
        var token = await _db.RefreshTokens
            .FirstOrDefaultAsync(t => t.TokenHash == presentedHash, cancellationToken);

        // Idempotent: revoking an unknown or already-revoked token still succeeds, so a
        // client can safely log out without leaking whether the token existed.
        if (token is not null && token.IsActive(now))
        {
            token.Revoke(now);
            await _db.SaveChangesAsync(cancellationToken);
        }

        return Result.Success();
    }
}
