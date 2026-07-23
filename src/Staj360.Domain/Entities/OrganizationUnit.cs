using Staj360.Domain.Common;
using Staj360.Domain.Enums;

namespace Staj360.Domain.Entities;

/// <summary>
/// Sabit teşkilat birimi (daire başkanlığı veya şube müdürlüğü).
/// CRUD yok; yalnızca seed ile yönetilir.
/// </summary>
public class OrganizationUnit : AuditableEntity
{
    /// <summary>Idempotent seed için kararlı kod (ör. PARK_BAHCE_BAKIM).</summary>
    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;
    public OrganizationUnitType UnitType { get; set; }
    public Guid? ParentId { get; set; }
    public OrganizationUnit? Parent { get; set; }
    public int DisplayOrder { get; set; }
    public bool IsActive { get; set; } = true;

    public ICollection<OrganizationUnit> Children { get; set; } = new List<OrganizationUnit>();
    public ICollection<ManagerUnitAssignment> ManagerAssignments { get; set; } = new List<ManagerUnitAssignment>();
    public ICollection<AdvisorUnitAssignment> AdvisorAssignments { get; set; } = new List<AdvisorUnitAssignment>();
    public ICollection<InternUnitAssignment> InternAssignments { get; set; } = new List<InternUnitAssignment>();
    public ICollection<InternProfile> Interns { get; set; } = new List<InternProfile>();
    public ICollection<Project> Projects { get; set; } = new List<Project>();
}
