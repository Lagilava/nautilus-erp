namespace ERP.Application.Common.Models;

/// <summary>Admin-facing view of a user account, including whether it is currently active.</summary>
public sealed record UserAccount(
    Guid Id,
    string Email,
    string FirstName,
    string LastName,
    IReadOnlyList<string> Roles,
    bool IsActive);
