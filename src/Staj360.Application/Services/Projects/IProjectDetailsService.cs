using Staj360.Application.Common;
using Staj360.Domain.Enums;

namespace Staj360.Application.Services.Projects;

public enum ProjectViewerKind
{
    Admin = 0,
    Mentor = 1,
    Intern = 2,
    Manager = 3
}

public class ProjectDetailsDto
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? Description { get; init; }
    public ProjectStatus Status { get; init; }
    public DateOnly StartDate { get; init; }
    public DateOnly? EndDate { get; init; }
    public DateTime CreatedAtUtc { get; init; }
    public string OrganizationUnitName { get; init; } = string.Empty;
    public string MentorFullName { get; init; } = string.Empty;
    public int ProgressPercentage { get; init; }
    public string? RepositoryUrl { get; init; }
    public string? TechnologiesSummary { get; init; }
    public bool CanManage { get; init; }

    public IReadOnlyList<ProjectTeamMemberDto> Team { get; init; } = Array.Empty<ProjectTeamMemberDto>();
    public IReadOnlyList<ProjectTaskItemDto> Tasks { get; init; } = Array.Empty<ProjectTaskItemDto>();
    public IReadOnlyList<ProjectLinkedReportDto> DailyReports { get; init; } = Array.Empty<ProjectLinkedReportDto>();
    public ProjectSummaryDto Summary { get; init; } = new();
}

public class ProjectTeamMemberDto
{
    public Guid AssignmentId { get; init; }
    public Guid InternProfileId { get; init; }
    public string FullName { get; init; } = string.Empty;
    public string? RoleDescription { get; init; }
    public string? PeriodLabel { get; init; }
    public InternshipStatus? PeriodStatus { get; init; }
}

public class ProjectTaskItemDto
{
    public Guid Id { get; init; }
    public string Title { get; init; } = string.Empty;
    public string? AssignedInternFullName { get; init; }
    public ProjectTaskStatus Status { get; init; }
    public TaskPriority Priority { get; init; }
    public DateOnly? DueDate { get; init; }
    public DateTime? CompletedAtUtc { get; init; }
}

public class ProjectLinkedReportDto
{
    public Guid ReportId { get; init; }
    public string InternFullName { get; init; } = string.Empty;
    public DateOnly ReportDate { get; init; }
    public string WorkSummary { get; init; } = string.Empty;
    public int TotalDurationMinutes { get; init; }
    public DailyReportStatus Status { get; init; }
}

public class ProjectSummaryDto
{
    public int TotalTasks { get; init; }
    public int CompletedTasks { get; init; }
    public int PendingTasks { get; init; }
    public int TotalDailyReports { get; init; }
    public int TeamMemberCount { get; init; }
}

public interface IProjectDetailsService
{
    /// <summary>
    /// Proje detayını roller arası ortak DTO olarak döner.
    /// Intern tüm projeleri okuyabilir; yönetme yalnızca Admin veya sorumlu Mentor.
    /// </summary>
    Task<ServiceResult<ProjectDetailsDto>> GetDetailsAsync(
        Guid projectId,
        Guid actingUserId,
        ProjectViewerKind viewer,
        CancellationToken cancellationToken = default);
}
