using Staj360.Domain.Common;

namespace Staj360.Domain.Entities;

/// <summary>Yönetici ↔ şube müdürlüğü çoktan çoğa ataması.</summary>
public class ManagerUnitAssignment : AuditableEntity
{
    public Guid ManagerUserId { get; set; }
    public Guid OrganizationUnitId { get; set; }
    public OrganizationUnit? OrganizationUnit { get; set; }

    public bool IsActive { get; set; } = true;
    public DateTime AssignedAtUtc { get; set; }
    public DateTime? EndedAtUtc { get; set; }
}
