namespace Staj360.Application.Abstractions;

/// <summary>Aktif isteğin kullanıcı bağlamını sağlar.</summary>
public interface ICurrentUserService
{
    Guid? UserId { get; }
    string? UserName { get; }
    string? IpAddress { get; }
    string? UserAgent { get; }
    string? RequestMethod { get; }
    string? RequestPath { get; }
    string? CorrelationId { get; }
    string? PrimaryRole { get; }
    bool IsAuthenticated { get; }
    bool IsInRole(string role);
}
