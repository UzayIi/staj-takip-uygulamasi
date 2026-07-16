using Staj360.Domain.Common;

namespace Staj360.Domain.Entities;

public class ProjectAssignment : AuditableEntity
{
    public Guid ProjectId { get; set; }
    public Project? Project { get; set; }

    public Guid InternProfileId { get; set; }
    public InternProfile? InternProfile { get; set; }

    public DateTime AssignedAtUtc { get; set; }
    public string? RoleDescription { get; set; }

    // Aynı stajyer aynı projeye iki kez aktif atanamaz (filtered unique index).
    public bool IsActive { get; set; } = true;
}
