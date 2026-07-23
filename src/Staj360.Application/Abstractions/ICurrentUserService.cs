namespace Staj360.Application.Abstractions;

/// <summary>Aktif isteğin kullanıcı bağlamını sağlar.</summary>
public interface ICurrentUserService
{
    Guid? UserId { get; }
    string? UserName { get; }
    string? IpAddress { get; }
    bool IsAuthenticated { get; }
    bool IsInRole(string role);
}
