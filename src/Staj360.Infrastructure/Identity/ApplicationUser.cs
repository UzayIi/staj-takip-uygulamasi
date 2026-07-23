using Microsoft.AspNetCore.Identity;

namespace Staj360.Infrastructure.Identity;

/// <summary>
/// ASP.NET Core Identity kullanıcısı. Identity'ye ait olduğu için Infrastructure
/// katmanındadır. Domain kayıtları bu tipe değil Guid UserId'ye bağlanır.
/// </summary>
public class ApplicationUser : IdentityUser<Guid>
{
    public string FullName { get; set; } = string.Empty;

    // Pasif kullanıcı sisteme giriş yapamaz.
    public bool IsActive { get; set; } = true;

    public DateTime CreatedAtUtc { get; set; }
}

public class ApplicationRole : IdentityRole<Guid>
{
    public ApplicationRole() { }
    public ApplicationRole(string roleName) : base(roleName) { }
}
