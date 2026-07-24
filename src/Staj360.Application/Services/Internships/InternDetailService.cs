using Microsoft.EntityFrameworkCore;
using Staj360.Application.Abstractions;
using Staj360.Application.Common;
using Staj360.Application.Services.Assignments;
using Staj360.Domain.Enums;

namespace Staj360.Application.Services.Internships;

public class InternDetailService : IInternDetailService
{
    private readonly IApplicationDbContext _db;
    private readonly IUserDisplayLookup _users;
    private readonly IUnitAssignmentService _assignments;

    public InternDetailService(
        IApplicationDbContext db,
        IUserDisplayLookup users,
        IUnitAssignmentService assignments)
    {
        _db = db;
        _users = users;
        _assignments = assignments;
    }

    public async Task<ServiceResult<InternDetailDto>> GetForViewerAsync(
        Guid actingUserId,
        bool isAdmin,
        bool isManager,
        bool isMentor,
        Guid internProfileId,
        CancellationToken cancellationToken = default)
    {
        var profile = await _db.InternProfiles.AsNoTracking()
            .Include(p => p.CurrentOrganizationUnit)!.ThenInclude(u => u!.Parent)
            .FirstOrDefaultAsync(p => p.Id == internProfileId && !p.IsDeleted, cancellationToken);
        if (profile is null)
            return ServiceResult<InternDetailDto>.Fail("Stajyer bulunamadı.", "NOT_FOUND");

        if (!isAdmin)
        {
            var allowed = false;
            if (isManager)
            {
                var unitIds = await _assignments.GetManagerUnitIdsAsync(actingUserId, cancellationToken);
                allowed = unitIds.Contains(profile.CurrentOrganizationUnitId);
            }

            if (!allowed && isMentor)
            {
                var mentorOfPeriod = await _db.InternshipPeriods.AsNoTracking()
                    .AnyAsync(p => p.InternProfileId == internProfileId
                                   && p.Status == InternshipStatus.Active
                                   && p.MentorUserId == actingUserId
                                   && !p.IsDeleted, cancellationToken);
                var advisorOfAssignment = await _db.InternUnitAssignments.AsNoTracking()
                    .AnyAsync(a => a.InternProfileId == internProfileId
                                   && a.IsActive
                                   && !a.IsDeleted
                                   && a.AdvisorUserId == actingUserId, cancellationToken);
                allowed = mentorOfPeriod || advisorOfAssignment;
            }

            if (!allowed)
                return ServiceResult<InternDetailDto>.Fail("Bu stajyere erişim yetkiniz yok.", "FORBIDDEN");
        }

        var userMap = await _users.GetByIdsAsync(new[] { profile.UserId }, cancellationToken);
        userMap.TryGetValue(profile.UserId, out var userInfo);
        var fullName = string.IsNullOrWhiteSpace(userInfo?.FullName) ? "(İsimsiz)" : userInfo!.FullName;
        var email = userInfo?.Email ?? string.Empty;

        var period = await _db.InternshipPeriods.AsNoTracking()
            .Where(p => p.InternProfileId == internProfileId && !p.IsDeleted)
            .OrderByDescending(p => p.Status == InternshipStatus.Active)
            .ThenByDescending(p => p.StartDate)
            .FirstOrDefaultAsync(cancellationToken);

        string? mentorName = null;
        if (period is not null)
        {
            var mentors = await _users.GetByIdsAsync(new[] { period.MentorUserId }, cancellationToken);
            mentors.TryGetValue(period.MentorUserId, out var m);
            mentorName = m?.FullName;
        }

        var periodIds = await _db.InternshipPeriods.AsNoTracking()
            .Where(p => p.InternProfileId == internProfileId && !p.IsDeleted)
            .Select(p => p.Id)
            .ToListAsync(cancellationToken);

        var projectRows = await (
            from a in _db.ProjectAssignments.AsNoTracking()
            join p in _db.Projects.AsNoTracking() on a.ProjectId equals p.Id
            where a.InternProfileId == internProfileId && a.IsActive && !a.IsDeleted && !p.IsDeleted
            select new InternDetailProjectDto(p.Id, p.Name, p.ProgressPercentage, p.Status)
        ).ToListAsync(cancellationToken);

        var tasks = await _db.ProjectTasks.AsNoTracking()
            .Include(t => t.Project)
            .Where(t => t.AssignedInternProfileId == internProfileId && !t.IsDeleted)
            .OrderByDescending(t => t.DueDate)
            .Select(t => new InternDetailTaskDto(t.Id, t.Title, t.Status, t.DueDate, t.Project != null ? t.Project.Name : null))
            .Take(50)
            .ToListAsync(cancellationToken);

        var reports = periodIds.Count == 0
            ? new List<InternDetailReportDto>()
            : await _db.DailyReports.AsNoTracking()
                .Include(r => r.OrganizationUnit)
                .Where(r => periodIds.Contains(r.InternshipPeriodId) && !r.IsDeleted)
                .OrderByDescending(r => r.ReportDate)
                .Take(40)
                .Select(r => new InternDetailReportDto(r.Id, r.ReportDate, r.Status, r.OrganizationUnit != null ? r.OrganizationUnit.Name : null))
                .ToListAsync(cancellationToken);

        var attendanceDays = periodIds.Count == 0
            ? new List<AttendanceStatus>()
            : await _db.AttendanceDays.AsNoTracking()
                .Where(d => periodIds.Contains(d.InternshipPeriodId) && !d.IsDeleted)
                .Select(d => d.Status)
                .ToListAsync(cancellationToken);

        var attendance = new InternDetailAttendanceSummaryDto(
            PresentDays: attendanceDays.Count(s => s == AttendanceStatus.Present),
            LateDays: attendanceDays.Count(s => s == AttendanceStatus.Late),
            AbsentDays: attendanceDays.Count(s => s == AttendanceStatus.Absent),
            IncompleteDays: attendanceDays.Count(s => s == AttendanceStatus.Incomplete),
            OnLeaveDays: attendanceDays.Count(s => s == AttendanceStatus.OnLeave),
            TotalRecordedDays: attendanceDays.Count);

        var leaves = periodIds.Count == 0
            ? new List<InternDetailLeaveDto>()
            : await _db.LeaveRequests.AsNoTracking()
                .Where(l => periodIds.Contains(l.InternshipPeriodId) && !l.IsDeleted)
                .OrderByDescending(l => l.StartDate)
                .Select(l => new InternDetailLeaveDto(l.Id, l.LeaveType, l.StartDate, l.EndDate, l.Status, l.Reason))
                .ToListAsync(cancellationToken);

        var evaluationEntities = periodIds.Count == 0
            ? []
            : await _db.Evaluations.AsNoTracking()
                .Where(e => periodIds.Contains(e.InternshipPeriodId) && !e.IsDeleted)
                .OrderByDescending(e => e.EvaluationDate)
                .ToListAsync(cancellationToken);
        var evaluations = evaluationEntities
            .Select(e => new InternDetailEvaluationDto(e.Id, e.EvaluationDate, e.AverageScore, e.GeneralComment))
            .ToList();

        var transfers = await _db.InternTransferRequests.AsNoTracking()
            .Include(t => t.SourceOrganizationUnit)
            .Include(t => t.TargetOrganizationUnit)
            .Where(t => t.InternProfileId == internProfileId && !t.IsDeleted)
            .OrderByDescending(t => t.CreatedAtUtc)
            .Select(t => new InternDetailTransferDto(
                t.Id,
                t.SourceOrganizationUnit != null ? t.SourceOrganizationUnit.Name : "—",
                t.TargetOrganizationUnit != null ? t.TargetOrganizationUnit.Name : "—",
                t.Status,
                t.PlannedStartDate,
                t.CreatedAtUtc))
            .ToListAsync(cancellationToken);

        var unitHistoryRaw = await _db.InternUnitAssignments.AsNoTracking()
            .Include(a => a.OrganizationUnit)
            .Where(a => a.InternProfileId == internProfileId && !a.IsDeleted)
            .OrderByDescending(a => a.IsActive)
            .ThenByDescending(a => a.StartDate)
            .ToListAsync(cancellationToken);

        var advisorIds = unitHistoryRaw.Select(a => a.AdvisorUserId).Distinct().ToList();
        var advisorMap = await _users.GetByIdsAsync(advisorIds, cancellationToken);
        var unitHistory = unitHistoryRaw.Select(a =>
        {
            advisorMap.TryGetValue(a.AdvisorUserId, out var adv);
            return new InternDetailUnitHistoryDto(
                a.Id,
                a.OrganizationUnit?.Name ?? "—",
                a.StartDate,
                a.EndDate,
                a.IsActive,
                adv?.FullName);
        }).ToList();

        var dto = new InternDetailDto
        {
            InternProfileId = profile.Id,
            UserId = profile.UserId,
            FullName = fullName,
            Email = email,
            StudentNumber = profile.StudentNumber,
            NationalIdMasked = MaskNationalId(profile.NationalId),
            University = profile.University,
            Faculty = profile.Faculty,
            SchoolDepartment = profile.SchoolDepartment,
            ClassLevel = profile.ClassLevel,
            PhoneNumber = profile.PhoneNumber,
            Address = profile.Address,
            EmergencyContactName = profile.EmergencyContactName,
            EmergencyContactPhone = profile.EmergencyContactPhone,
            OrganizationUnitId = profile.CurrentOrganizationUnitId,
            OrganizationUnitName = profile.CurrentOrganizationUnit?.Name ?? "—",
            DirectorateName = profile.CurrentOrganizationUnit?.Parent?.Name,
            MentorFullName = mentorName,
            PeriodStartDate = period?.StartDate,
            PeriodEndDate = period?.EndDate,
            PeriodStatus = period?.Status,
            IsActive = profile.IsActive,
            Projects = projectRows,
            Tasks = tasks,
            DailyReports = reports,
            Attendance = attendance,
            Leaves = leaves,
            Evaluations = evaluations,
            Transfers = transfers,
            UnitHistory = unitHistory
        };

        return ServiceResult<InternDetailDto>.Ok(dto);
    }

    private static string? MaskNationalId(string? nationalId)
    {
        if (string.IsNullOrWhiteSpace(nationalId) || nationalId.Length < 5)
            return null;
        return $"{nationalId[..3]}******{nationalId[^2..]}";
    }
}
