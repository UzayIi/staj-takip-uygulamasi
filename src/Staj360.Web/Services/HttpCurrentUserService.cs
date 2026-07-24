using System.Net;
using System.Security.Claims;
using Staj360.Application.Abstractions;
using Staj360.Application.Common;

namespace Staj360.Web.Services;

/// <summary>ICurrentUserService'in HttpContext tabanlı implementasyonu.</summary>
public class HttpCurrentUserService : ICurrentUserService
{
    private readonly IHttpContextAccessor _accessor;

    public HttpCurrentUserService(IHttpContextAccessor accessor) => _accessor = accessor;

    private HttpContext? Http => _accessor.HttpContext;
    private ClaimsPrincipal? User => Http?.User;

    public Guid? UserId
    {
        get
        {
            var value = User?.FindFirstValue(ClaimTypes.NameIdentifier);
            return Guid.TryParse(value, out var id) ? id : null;
        }
    }

    public string? UserName => User?.FindFirstValue("FullName")
        ?? User?.FindFirstValue(ClaimTypes.Name)
        ?? User?.Identity?.Name;

    public string? IpAddress
    {
        get
        {
            var remote = Http?.Connection.RemoteIpAddress;
            if (remote is null) return null;
            if (remote.IsIPv4MappedToIPv6)
                remote = remote.MapToIPv4();
            return remote.ToString();
        }
    }

    public string? UserAgent => Http?.Request.Headers.UserAgent.ToString();
    public string? RequestMethod => Http?.Request.Method;
    public string? RequestPath => Http?.Request.Path.Value;
    public string? CorrelationId => Http?.TraceIdentifier;

    public string? PrimaryRole
    {
        get
        {
            if (User is null) return null;
            foreach (var role in new[] { AppRoles.SuperAdmin, AppRoles.Admin, AppRoles.Manager, AppRoles.Mentor, AppRoles.Intern })
            {
                if (User.IsInRole(role)) return role;
            }
            return User.FindFirstValue(ClaimTypes.Role);
        }
    }

    public bool IsAuthenticated => User?.Identity?.IsAuthenticated ?? false;

    public bool IsInRole(string role) => User?.IsInRole(role) ?? false;
}
