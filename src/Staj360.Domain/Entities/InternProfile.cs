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
    public string? Address { get; set; }
    public string? EmergencyContactName { get; set; }
    public string? EmergencyContactPhone { get; set; }

    /// <summary>Aktif şube (denormalize; kaynak InternUnitAssignment).</summary>
    public Guid CurrentOrganizationUnitId { get; set; }
    public OrganizationUnit? CurrentOrganizationUnit { get; set; }

    public bool IsActive { get; set; } = true;

    public ICollection<InternshipPeriod> InternshipPeriods { get; set; } = new List<InternshipPeriod>();
    public ICollection<ProjectAssignment> ProjectAssignments { get; set; } = new List<ProjectAssignment>();
    public ICollection<InternUnitAssignment> UnitAssignments { get; set; } = new List<InternUnitAssignment>();
    public ICollection<InternTransferRequest> TransferRequests { get; set; } = new List<InternTransferRequest>();
    public ICollection<InternFeedback> Feedbacks { get; set; } = new List<InternFeedback>();
}
