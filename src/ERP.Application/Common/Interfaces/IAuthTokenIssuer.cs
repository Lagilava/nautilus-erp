using ERP.Application.Common.Models;

namespace ERP.Application.Common.Interfaces;

/// <summary>
/// Issues an access + refresh token pair for an authenticated user and persists the
/// refresh token. Shared by the Login and Refresh handlers so token issuance and
/// rotation logic lives in exactly one place.
/// </summary>
public interface IAuthTokenIssuer
{
    Task<AuthenticationResult> IssueAsync(
        UserIdentity user, string? ipAddress, CancellationToken cancellationToken = default);
}
