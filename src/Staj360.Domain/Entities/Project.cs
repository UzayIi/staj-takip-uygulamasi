using Staj360.Domain.Common;
using Staj360.Domain.Enums;

namespace Staj360.Domain.Entities;

public class Project : AuditableEntity
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateOnly StartDate { get; set; }
    public DateOnly? EndDate { get; set; }

    public ProjectStatus Status { get; set; } = ProjectStatus.Planned;

    // 0-100 arası tamamlanma oranı (DB kısıtı ile korunur).
    public int ProgressPercentage { get; set; }

    public string? RepositoryUrl { get; set; }

    public Guid OrganizationUnitId { get; set; }
    public OrganizationUnit? OrganizationUnit { get; set; }

    public Guid MentorUserId { get; set; }

    public ICollection<ProjectAssignment> Assignments { get; set; } = new List<ProjectAssignment>();
    public ICollection<ProjectTask> Tasks { get; set; } = new List<ProjectTask>();
}
