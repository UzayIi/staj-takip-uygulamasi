using Staj360.Application.Common;
using Staj360.Domain.Enums;

namespace Staj360.Application.Services.Internships;

public class InternDetailDto
{
    public Guid InternProfileId { get; init; }
    public Guid UserId { get; init; }
    public string FullName { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public string StudentNumber { get; init; } = string.Empty;
    public string? NationalIdMasked { get; init; }
    public string? University { get; init; }
    public string? Faculty { get; init; }
    public string? SchoolDepartment { get; init; }
    public string? ClassLevel { get; init; }
    public string? PhoneNumber { get; init; }
    public string? Address { get; init; }
    public string? EmergencyContactName { get; init; }
    public string? EmergencyContactPhone { get; init; }
    public Guid OrganizationUnitId { get; init; }
    public string OrganizationUnitName { get; init; } = string.Empty;
    public string? DirectorateName { get; init; }
    public string? MentorFullName { get; init; }
    public DateOnly? PeriodStartDate { get; init; }
    public DateOnly? PeriodEndDate { get; init; }
    public InternshipStatus? PeriodStatus { get; init; }
    public bool IsActive { get; init; }

    public IReadOnlyList<InternDetailProjectDto> Projects { get; init; } = Array.Empty<InternDetailProjectDto>();
    public IReadOnlyList<InternDetailTaskDto> Tasks { get; init; } = Array.Empty<InternDetailTaskDto>();
    public IReadOnlyList<InternDetailReportDto> DailyReports { get; init; } = Array.Empty<InternDetailReportDto>();
    public InternDetailAttendanceSummaryDto Attendance { get; init; } = new();
    public IReadOnlyList<InternDetailLeaveDto> Leaves { get; init; } = Array.Empty<InternDetailLeaveDto>();
    public IReadOnlyList<InternDetailEvaluationDto> Evaluations { get; init; } = Array.Empty<InternDetailEvaluationDto>();
    public IReadOnlyList<InternDetailTransferDto> Transfers { get; init; } = Array.Empty<InternDetailTransferDto>();
    public IReadOnlyList<InternDetailUnitHistoryDto> UnitHistory { get; init; } = Array.Empty<InternDetailUnitHistoryDto>();
}

public record InternDetailProjectDto(Guid ProjectId, string Name, int ProgressPercentage, ProjectStatus Status);
public record InternDetailTaskDto(Guid TaskId, string Title, ProjectTaskStatus Status, DateOnly? DueDate, string? ProjectName);
public record InternDetailReportDto(Guid ReportId, DateOnly ReportDate, DailyReportStatus Status, string? UnitName);
public record InternDetailAttendanceSummaryDto(
    int PresentDays = 0,
    int LateDays = 0,
    int AbsentDays = 0,
    int IncompleteDays = 0,
    int OnLeaveDays = 0,
    int TotalRecordedDays = 0);
public record InternDetailLeaveDto(Guid LeaveId, LeaveType LeaveType, DateOnly StartDate, DateOnly EndDate, LeaveRequestStatus Status, string Reason);
public record InternDetailEvaluationDto(Guid EvaluationId, DateOnly EvaluationDate, double AverageScore, string? GeneralComment);
public record InternDetailTransferDto(
    Guid TransferId,
    string SourceUnitName,
    string TargetUnitName,
    TransferRequestStatus Status,
    DateOnly? PlannedStartDate,
    DateTime CreatedAtUtc);
public record InternDetailUnitHistoryDto(
    Guid AssignmentId,
    string UnitName,
    DateOnly StartDate,
    DateOnly? EndDate,
    bool IsActive,
    string? AdvisorFullName);

public interface IInternDetailService
{
    Task<ServiceResult<InternDetailDto>> GetForViewerAsync(
        Guid actingUserId,
        bool isAdmin,
        bool isManager,
        bool isMentor,
        Guid internProfileId,
        CancellationToken cancellationToken = default);
}
