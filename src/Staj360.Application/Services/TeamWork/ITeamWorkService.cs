using Staj360.Application.Common;
using Staj360.Domain.Enums;

namespace Staj360.Application.Services.TeamWork;

public record TeamProjectFilter(string? InternName, ProjectStatus? Status, DateOnly? FromDate, DateOnly? ToDate);

public record TeamReportFilter(
    string? InternName,
    Guid? ProjectId,
    DateOnly? FromDate,
    DateOnly? ToDate,
    DailyReportStatus? Status);

public class TeamProjectItemDto
{
    public Guid ProjectId { get; init; }
    public string InternFullName { get; init; } = string.Empty;
    public string ProjectName { get; init; } = string.Empty;
    public string? ProjectDescription { get; init; }
    public ProjectStatus Status { get; init; }
    public DateOnly StartDate { get; init; }
    public DateOnly? EndDate { get; init; }
    public string? RoleDescription { get; init; }
}

public class TeamReportItemDto
{
    public Guid ReportId { get; init; }
    public string InternFullName { get; init; } = string.Empty;
    public DateOnly ReportDate { get; init; }
    public string? ProjectName { get; init; }
    public string WorkSummary { get; init; } = string.Empty;
    public int TotalDurationMinutes { get; init; }
    public string? TechnologiesUsed { get; init; }
    public DailyReportStatus Status { get; init; }
}

public class TeamReportDetailDto
{
    public Guid ReportId { get; init; }
    public string InternFullName { get; init; } = string.Empty;
    public DateOnly ReportDate { get; init; }
    public string? ProjectName { get; init; }
    public string WorkSummary { get; init; } = string.Empty;
    public string? ProblemsEncountered { get; init; }
    public string? SolutionsApplied { get; init; }
    public int TotalDurationMinutes { get; init; }
    public string? TechnologiesUsed { get; init; }
    public DailyReportStatus Status { get; init; }
    public IReadOnlyList<TeamReportWorkItemDto> WorkItems { get; init; } = Array.Empty<TeamReportWorkItemDto>();
}

public class TeamReportWorkItemDto
{
    public string Title { get; init; } = string.Empty;
    public string? Description { get; init; }
    public int DurationMinutes { get; init; }
    public string? TechnologiesUsed { get; init; }
    public string? ProjectName { get; init; }
}

/// <summary>
/// Stajyerlerin birbirlerinin proje ve paylaşılabilir günlük raporlarını salt-okunur görmesi.
/// Taslak/reddedilmiş raporlar ve mentor notları dahil edilmez. PII yoktur.
/// </summary>
public interface ITeamWorkService
{
    Task<PagedResult<TeamProjectItemDto>> ListPeerProjectsAsync(
        TeamProjectFilter filter, int page, int pageSize, CancellationToken cancellationToken = default);

    Task<PagedResult<TeamReportItemDto>> ListPeerReportsAsync(
        TeamReportFilter filter, int page, int pageSize, CancellationToken cancellationToken = default);

    Task<ServiceResult<TeamReportDetailDto>> GetPeerReportDetailAsync(
        Guid reportId, Guid viewerUserId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<(Guid Id, string Name)>> ListProjectsForFilterAsync(CancellationToken cancellationToken = default);
}
