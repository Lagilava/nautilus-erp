using System.Security.Claims;
using ERP.Application.Common.Interfaces;
using Microsoft.AspNetCore.Http;

namespace ERP.Infrastructure.Services;

/// <summary>Reads the caller's identity and request metadata from the current HTTP context.</summary>
public sealed class CurrentUserService : ICurrentUserService
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentUserService(IHttpContextAccessor httpContextAccessor)
        => _httpContextAccessor = httpContextAccessor;

    private HttpContext? Context => _httpContextAccessor.HttpContext;

    public Guid? UserId
    {
        get
        {
            var value = Context?.User.FindFirstValue(ClaimTypes.NameIdentifier);
            return Guid.TryParse(value, out var id) ? id : null;
        }
    }

    public string? Email => Context?.User.FindFirstValue(ClaimTypes.Email);

    public Guid? BranchId
    {
        get
        {
            var value = Context?.User.FindFirstValue(Identity.TokenService.BranchClaim);
            return Guid.TryParse(value, out var id) ? id : null;
        }
    }

    public bool IsAuthenticated => Context?.User.Identity?.IsAuthenticated ?? false;

    // Available even for anonymous requests (login/register), so we can audit attempts.
    public string? IpAddress => Context?.Connection.RemoteIpAddress?.ToString();

    public string? UserAgent => Context?.Request.Headers.UserAgent.ToString();
}
