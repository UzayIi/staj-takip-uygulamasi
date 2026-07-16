using Staj360.Domain.Common;

namespace Staj360.Domain.Entities;

public class DailyWorkItem : AuditableEntity
{
    public Guid DailyReportId { get; set; }
    public DailyReport? DailyReport { get; set; }

    public Guid? ProjectId { get; set; }
    public Project? Project { get; set; }

    public Guid? ProjectTaskId { get; set; }
    public ProjectTask? ProjectTask { get; set; }

    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int DurationMinutes { get; set; }
    public string? TechnologiesUsed { get; set; }
    public string? Result { get; set; }
    public string? RepositoryUrl { get; set; }
}
