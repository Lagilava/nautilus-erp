using ERP.Application.Common.Models;
using ERP.Shared.Results;

namespace ERP.Application.Common.Interfaces;

/// <summary>
/// Abstracts ASP.NET Identity (UserManager/SignInManager) so Application handlers stay
/// free of framework types. Implemented in Persistence, which owns the Identity stores.
/// Expected/validation failures are returned as <see cref="Result"/>; only exceptional
/// conditions throw.
/// </summary>
public interface IIdentityService
{
    Task<Result<UserIdentity>> CreateUserAsync(
        string email, string password, string firstName, string lastName,
        IEnumerable<string> roles, Guid? branchId = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Verifies credentials, honouring lockout. Returns the user on success, or a
    /// failure whose <see cref="Error.Code"/> distinguishes bad credentials from lockout.
    /// </summary>
    Task<Result<UserIdentity>> ValidateCredentialsAsync(
        string email, string password, CancellationToken cancellationToken = default);

    Task<Result<UserIdentity>> GetByIdAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// True when the account is locked out — either by failed sign-in attempts or because an
    /// administrator deactivated it. The refresh path consults this so a disabled account cannot
    /// keep rotating tokens until its refresh token expires.
    /// </summary>
    Task<bool> IsLockedOutAsync(Guid userId, CancellationToken cancellationToken = default);

    Task<Result<string>> GeneratePasswordResetTokenAsync(
        string email, CancellationToken cancellationToken = default);

    Task<Result> ResetPasswordAsync(
        string email, string token, string newPassword, CancellationToken cancellationToken = default);

    /// <summary>Idempotently ensures a role exists (used during seeding/registration).</summary>
    Task EnsureRoleAsync(string role, CancellationToken cancellationToken = default);

    // --- Self-service (the user acting on their own account) ---

    /// <summary>Updates the caller's own display details. Cannot touch email, roles, or branch.</summary>
    Task<Result> UpdateProfileAsync(
        Guid userId, string firstName, string lastName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Changes the caller's password. Requires the current password — a logged-in session is
    /// not sufficient authority to replace a credential.
    /// </summary>
    Task<Result> ChangePasswordAsync(
        Guid userId, string currentPassword, string newPassword, CancellationToken cancellationToken = default);

    // --- Administration (Milestone: system administration) ---

    /// <summary>All user accounts with their roles and active state.</summary>
    Task<IReadOnlyList<UserAccount>> GetUsersAsync(CancellationToken cancellationToken = default);

    /// <summary>Replaces a user's role assignments with the supplied set.</summary>
    Task<Result> SetUserRolesAsync(
        Guid userId, IEnumerable<string> roles, CancellationToken cancellationToken = default);

    /// <summary>Enables or disables sign-in for a user (disable = indefinite lockout).</summary>
    Task<Result> SetUserActiveAsync(Guid userId, bool active, CancellationToken cancellationToken = default);

    /// <summary>Scopes a user to a branch. Null means unrestricted (sees every branch).</summary>
    Task<Result> SetUserBranchAsync(Guid userId, Guid? branchId, CancellationToken cancellationToken = default);
}
