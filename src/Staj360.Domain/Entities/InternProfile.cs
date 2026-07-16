using Staj360.Domain.Common;

namespace Staj360.Domain.Entities;

public class InternProfile : AuditableEntity
{
    // Identity kullanıcısına bağlantı (Domain Identity'ye bağımlı değildir).
    public Guid UserId { get; set; }

    public string StudentNumber { get; set; } = string.Empty;

    // Hassas veri: maskeli gösterilir, loglara ve AI'ya gönderilmez.
    public string? NationalId { get; set; }

    public string? University { get; set; }
    public string? Faculty { get; set; }
    public string? SchoolDepartment { get; set; }
    public string? ClassLevel { get; set; }
    public string? PhoneNumber { get; set; }
    public string? EmergencyContactName { get; set; }
    public string? EmergencyContactPhone { get; set; }

    public Guid DepartmentId { get; set; }
    public Department? Department { get; set; }

    public bool IsActive { get; set; } = true;

    public ICollection<InternshipPeriod> InternshipPeriods { get; set; } = new List<InternshipPeriod>();
    public ICollection<ProjectAssignment> ProjectAssignments { get; set; } = new List<ProjectAssignment>();
}
