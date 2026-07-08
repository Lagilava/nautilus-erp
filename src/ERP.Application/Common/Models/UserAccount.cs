namespace ERP.Application.Common.Models;

/// <summary>Admin-facing view of a user account, including branch scope and active state.</summary>
public sealed record UserAccount(
    Guid Id,
    string Email,
    string FirstName,
    string LastName,
    IReadOnlyList<string> Roles,
    bool IsActive,
    Guid? BranchId,
    string? BranchName);
