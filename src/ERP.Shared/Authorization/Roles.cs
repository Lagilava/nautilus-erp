namespace ERP.Shared.Authorization;

/// <summary>
/// Canonical role names, referenced by both authorization policies and seeding so the
/// strings never drift. Kept in the dependency-free shared kernel.
/// </summary>
public static class Roles
{
    public const string Administrator = "Administrator";
    public const string Manager = "Manager";
    public const string Staff = "Staff";

    public static readonly IReadOnlyList<string> All = new[] { Administrator, Manager, Staff };
}
