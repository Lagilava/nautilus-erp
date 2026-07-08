namespace ERP.Application.Common.Interfaces;

/// <summary>
/// Ambient information about the caller of the current request. Implemented in
/// Infrastructure over <c>IHttpContextAccessor</c>. Values are null for anonymous
/// requests and background work.
/// </summary>
public interface ICurrentUserService
{
    Guid? UserId { get; }
    string? Email { get; }
    bool IsAuthenticated { get; }
    string? IpAddress { get; }
    string? UserAgent { get; }

    /// <summary>
    /// Branch the caller is scoped to, from the "branch" claim. Null means unrestricted —
    /// the caller sees every branch's records.
    /// </summary>
    Guid? BranchId { get; }
}
