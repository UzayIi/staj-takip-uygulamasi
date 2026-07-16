namespace Staj360.Application.Common;

/// <summary>Rol adları magic string olarak dağılmasın diye tek yerde tutulur.</summary>
public static class AppRoles
{
    public const string SuperAdmin = "SuperAdmin";
    public const string Admin = "Admin";
    public const string Mentor = "Mentor";
    public const string Intern = "Intern";

    public static readonly string[] All = { SuperAdmin, Admin, Mentor, Intern };

    // Yaygın kullanılan rol kombinasyonları.
    public const string AdminOrSuperAdmin = SuperAdmin + "," + Admin;
    public const string StaffRoles = SuperAdmin + "," + Admin + "," + Mentor;
}
