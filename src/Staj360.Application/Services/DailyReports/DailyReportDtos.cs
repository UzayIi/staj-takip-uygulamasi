namespace Staj360.Application.Services.DailyReports;

/// <summary>
/// Rapor tarihi istemciden alınmaz; sunucu Europe/Istanbul gününü kullanır.
/// </summary>
public record CreateDailyReportCommand(
    string? GeneralNotes,
    string? ProblemsEncountered,
    string? SolutionsApplied,
    string? TomorrowPlan);

public record UpdateDailyReportCommand(
    Guid ReportId,
    string? GeneralNotes,
    string? ProblemsEncountered,
    string? SolutionsApplied,
    string? TomorrowPlan);

public record AddWorkItemCommand(
    Guid ReportId,
    Guid? ProjectId,
    Guid? ProjectTaskId,
    string Title,
    string? Description,
    int DurationMinutes,
    string? TechnologiesUsed,
    string? Result,
    string? RepositoryUrl);

public enum ReviewDecision
{
    Approve = 0,
    RequestRevision = 1,
    Reject = 2
}

public record ReviewDailyReportCommand(
    Guid ReportId,
    ReviewDecision Decision,
    string? MentorComment);
