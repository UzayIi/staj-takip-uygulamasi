using Staj360.Domain.Common;
using Staj360.Domain.Enums;

namespace Staj360.Domain.Entities;

public class ProjectTask : AuditableEntity
{
    public Guid ProjectId { get; set; }
    public Project? Project { get; set; }

    public Guid? AssignedInternProfileId { get; set; }
    public InternProfile? AssignedInternProfile { get; set; }

    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public TaskPriority Priority { get; set; } = TaskPriority.Medium;
    public ProjectTaskStatus Status { get; set; } = ProjectTaskStatus.Todo;
    public DateOnly? DueDate { get; set; }
    public int? EstimatedMinutes { get; set; }
    public DateTime? CompletedAtUtc { get; set; }
}
