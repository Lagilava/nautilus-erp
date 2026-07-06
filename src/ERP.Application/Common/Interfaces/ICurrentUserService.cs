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
}
