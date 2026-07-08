using ERP.Application.Common.Interfaces;
using ERP.Application.Common.Models;
using ERP.Shared.Results;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace ERP.Persistence.Identity;

/// <summary>
/// Implements the Application's <see cref="IIdentityService"/> port over ASP.NET Identity.
/// Lives in Persistence because it owns the Identity stores/managers. Translates Identity
/// results into the Application's <see cref="Result"/> vocabulary.
/// </summary>
public sealed class IdentityService : IIdentityService
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly RoleManager<ApplicationRole> _roleManager;
    private readonly ApplicationDbContext _db;

    public IdentityService(
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        RoleManager<ApplicationRole> roleManager,
        ApplicationDbContext db)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _roleManager = roleManager;
        _db = db;
    }

    public async Task<Result<UserIdentity>> CreateUserAsync(
        string email, string password, string firstName, string lastName,
        IEnumerable<string> roles, Guid? branchId = null, CancellationToken cancellationToken = default)
    {
        if (await _userManager.FindByEmailAsync(email) is not null)
            return Result.Failure<UserIdentity>(Error.Conflict("A user with this email already exists."));

        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            FirstName = firstName,
            LastName = lastName,
            BranchId = branchId
        };

        var created = await _userManager.CreateAsync(user, password);
        if (!created.Succeeded)
            return Result.Failure<UserIdentity>(ToError(created));

        var roleList = roles.ToArray();
        if (roleList.Length > 0)
        {
            var roleResult = await _userManager.AddToRolesAsync(user, roleList);
            if (!roleResult.Succeeded)
                return Result.Failure<UserIdentity>(ToError(roleResult));
        }

        return Result.Success(await ToUserIdentityAsync(user));
    }

    public async Task<Result<UserIdentity>> ValidateCredentialsAsync(
        string email, string password, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByEmailAsync(email);
        // Uniform failure whether the email is unknown or the password is wrong — no enumeration.
        if (user is null)
            return Result.Failure<UserIdentity>(Error.Unauthorized("Invalid credentials."));

        // lockoutOnFailure: true increments the failed-attempt counter and locks the
        // account per the configured policy.
        var result = await _signInManager.CheckPasswordSignInAsync(user, password, lockoutOnFailure: true);

        if (result.IsLockedOut)
            return Result.Failure<UserIdentity>(
                new Error("locked_out", "Account is locked due to repeated failed attempts."));
        if (!result.Succeeded)
            return Result.Failure<UserIdentity>(Error.Unauthorized("Invalid credentials."));

        return Result.Success(await ToUserIdentityAsync(user));
    }

    public async Task<Result<UserIdentity>> GetByIdAsync(
        Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        return user is null
            ? Result.Failure<UserIdentity>(Error.NotFound("User not found."))
            : Result.Success(await ToUserIdentityAsync(user));
    }

    public async Task<bool> IsLockedOutAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        return user is not null && await _userManager.IsLockedOutAsync(user);
    }

    public async Task<Result<string>> GeneratePasswordResetTokenAsync(
        string email, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByEmailAsync(email);
        if (user is null)
            return Result.Failure<string>(Error.NotFound("User not found."));

        return Result.Success(await _userManager.GeneratePasswordResetTokenAsync(user));
    }

    public async Task<Result> ResetPasswordAsync(
        string email, string token, string newPassword, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByEmailAsync(email);
        if (user is null)
            return Result.Failure(Error.Unauthorized("Invalid reset request."));

        var result = await _userManager.ResetPasswordAsync(user, token, newPassword);
        return result.Succeeded ? Result.Success() : Result.Failure(ToError(result));
    }

    public async Task EnsureRoleAsync(string role, CancellationToken cancellationToken = default)
    {
        if (!await _roleManager.RoleExistsAsync(role))
            await _roleManager.CreateAsync(new ApplicationRole(role));
    }

    private async Task<UserIdentity> ToUserIdentityAsync(ApplicationUser user)
    {
        var roles = await _userManager.GetRolesAsync(user);
        return new UserIdentity(
            user.Id, user.Email ?? string.Empty, user.FirstName, user.LastName, roles.ToArray(), user.BranchId);
    }

    public async Task<IReadOnlyList<UserAccount>> GetUsersAsync(CancellationToken cancellationToken = default)
    {
        var users = await _userManager.Users.OrderBy(u => u.Email).ToListAsync(cancellationToken);
        var branches = await _db.Branches.AsNoTracking()
            .ToDictionaryAsync(b => b.Id, b => b.Name, cancellationToken);
        var now = DateTimeOffset.UtcNow;

        var accounts = new List<UserAccount>(users.Count);
        foreach (var user in users)
        {
            var roles = await _userManager.GetRolesAsync(user);
            // Active unless an indefinite lockout is in force.
            var isActive = user.LockoutEnd is null || user.LockoutEnd <= now;
            var branchName = user.BranchId is { } id && branches.TryGetValue(id, out var name) ? name : null;
            accounts.Add(new UserAccount(
                user.Id, user.Email ?? string.Empty, user.FirstName, user.LastName,
                roles.ToArray(), isActive, user.BranchId, branchName));
        }
        return accounts;
    }

    public async Task<Result> SetUserBranchAsync(Guid userId, Guid? branchId, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user is null) return Result.Failure(Error.NotFound("User not found."));

        if (branchId is { } id && !await _db.Branches.AnyAsync(b => b.Id == id, cancellationToken))
            return Result.Failure(Error.Validation("Branch does not exist."));

        user.BranchId = branchId;
        var result = await _userManager.UpdateAsync(user);
        return result.Succeeded ? Result.Success() : Result.Failure(ToError(result));
    }

    public async Task<Result> UpdateProfileAsync(
        Guid userId, string firstName, string lastName, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user is null) return Result.Failure(Error.NotFound("User not found."));

        // Deliberately does not touch Email, roles, or BranchId — those are privileged.
        user.FirstName = firstName;
        user.LastName = lastName;

        var result = await _userManager.UpdateAsync(user);
        return result.Succeeded ? Result.Success() : Result.Failure(ToError(result));
    }

    public async Task<Result> ChangePasswordAsync(
        Guid userId, string currentPassword, string newPassword, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user is null) return Result.Failure(Error.NotFound("User not found."));

        // ChangePasswordAsync verifies the current password; a live session alone is not enough.
        var result = await _userManager.ChangePasswordAsync(user, currentPassword, newPassword);
        if (!result.Succeeded)
            return Result.Failure(Error.Unauthorized("Current password is incorrect."));

        return Result.Success();
    }

    public async Task<Result> SetUserRolesAsync(
        Guid userId, IEnumerable<string> roles, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user is null) return Result.Failure(Error.NotFound("User not found."));

        var target = roles.Distinct().ToArray();
        foreach (var role in target) await EnsureRoleAsync(role, cancellationToken);

        var current = await _userManager.GetRolesAsync(user);
        var removed = await _userManager.RemoveFromRolesAsync(user, current.Except(target));
        if (!removed.Succeeded) return Result.Failure(ToError(removed));

        var added = await _userManager.AddToRolesAsync(user, target.Except(current));
        return added.Succeeded ? Result.Success() : Result.Failure(ToError(added));
    }

    public async Task<Result> SetUserActiveAsync(Guid userId, bool active, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user is null) return Result.Failure(Error.NotFound("User not found."));

        // Disable = lock out indefinitely; enable = clear the lockout end.
        var result = await _userManager.SetLockoutEndDateAsync(user, active ? null : DateTimeOffset.MaxValue);
        if (!result.Succeeded) return Result.Failure(ToError(result));

        // Locking the account only closes the login door. A deactivated user still holding a
        // refresh token would keep rotating it — and stay signed in — for its full lifetime, so
        // revoke the live tokens too. This is what makes "deactivate" take effect immediately.
        if (!active)
        {
            var now = DateTimeOffset.UtcNow;
            var live = await _db.RefreshTokens
                .Where(t => t.UserId == userId && t.RevokedAt == null)
                .ToListAsync(cancellationToken);

            foreach (var token in live) token.Revoke(now);
            await _db.SaveChangesAsync(cancellationToken);
        }

        return Result.Success();
    }

    private static Error ToError(IdentityResult result)
    {
        var message = string.Join(" ", result.Errors.Select(e => e.Description));
        return Error.Validation(string.IsNullOrWhiteSpace(message) ? "Identity operation failed." : message);
    }
}
