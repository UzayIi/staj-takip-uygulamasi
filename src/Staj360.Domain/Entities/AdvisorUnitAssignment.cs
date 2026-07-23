using Staj360.Domain.Common;

namespace Staj360.Domain.Entities;

/// <summary>Danışman ↔ şube müdürlüğü ataması.</summary>
public class AdvisorUnitAssignment : AuditableEntity
{
    public Guid AdvisorUserId { get; set; }
    public Guid OrganizationUnitId { get; set; }
    public OrganizationUnit? OrganizationUnit { get; set; }

    public bool IsActive { get; set; } = true;
    public DateTime AssignedAtUtc { get; set; }
    public DateTime? EndedAtUtc { get; set; }
}
