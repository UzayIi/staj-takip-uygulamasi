namespace Staj360.Application.Common;

/// <summary>Rol adları magic string olarak dağılmasın diye tek yerde tutulur.</summary>
public static class AppRoles
{
    public const string SuperAdmin = "SuperAdmin";
    public const string Admin = "Admin";
    /// <summary>Yönetici — müdürlük sorumlusu.</summary>
    public const string Manager = "Manager";
    /// <summary>Danışman (UI: Danışman).</summary>
    public const string Mentor = "Mentor";
    public const string Intern = "Intern";

    public static readonly string[] All = { SuperAdmin, Admin, Manager, Mentor, Intern };

    public const string AdminOrSuperAdmin = SuperAdmin + "," + Admin;
    public const string StaffRoles = SuperAdmin + "," + Admin + "," + Manager + "," + Mentor;
}
