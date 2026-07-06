using ERP.Application.Common.Interfaces;
using ERP.Application.Common.Models;
using ERP.Shared.Results;
using Microsoft.AspNetCore.Identity;

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

    public IdentityService(
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        RoleManager<ApplicationRole> roleManager)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _roleManager = roleManager;
    }

    public async Task<Result<UserIdentity>> CreateUserAsync(
        string email, string password, string firstName, string lastName,
        IEnumerable<string> roles, CancellationToken cancellationToken = default)
    {
        if (await _userManager.FindByEmailAsync(email) is not null)
            return Result.Failure<UserIdentity>(Error.Conflict("A user with this email already exists."));

        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            FirstName = firstName,
            LastName = lastName
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
            user.Id, user.Email ?? string.Empty, user.FirstName, user.LastName, roles.ToArray());
    }

    private static Error ToError(IdentityResult result)
    {
        var message = string.Join(" ", result.Errors.Select(e => e.Description));
        return Error.Validation(string.IsNullOrWhiteSpace(message) ? "Identity operation failed." : message);
    }
}
