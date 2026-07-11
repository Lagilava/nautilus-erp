namespace ERP.Application.Common.Models;

/// <summary>
/// A framework-agnostic projection of an identity user, so the Application layer
/// never depends on ASP.NET Identity's <c>ApplicationUser</c> (which lives in Persistence).
/// </summary>
public sealed record UserIdentity(
    Guid Id,
    string Email,
    string FirstName,
    string LastName,
    IReadOnlyList<string> Roles,
    Guid? BranchId = null,
    bool MfaEnabled = false);
