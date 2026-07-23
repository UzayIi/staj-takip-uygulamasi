using Microsoft.EntityFrameworkCore;
using Staj360.Application.Abstractions;
using Staj360.Application.Common;
using Staj360.Application.Services.Projects;
using Staj360.Domain.Enums;

namespace Staj360.Application.Services.Projects;

public class ProjectDetailsService : IProjectDetailsService
{
    private readonly IApplicationDbContext _db;
    private readonly IUserDisplayLookup _users;

    public ProjectDetailsService(IApplicationDbContext db, IUserDisplayLookup users)
    {
        _db = db;
        _users = users;
    }

    public async Task<ServiceResult<ProjectDetailsDto>> GetDetailsAsync(
        Guid projectId,
        Guid actingUserId,
        ProjectViewerKind viewer,
        CancellationToken cancellationToken = default)
    {
        var project = await _db.Projects.AsNoTracking()
            .Include(p => p.OrganizationUnit)
            .FirstOrDefaultAsync(p => p.Id == projectId && !p.IsDeleted, cancellationToken);

        if (project is null)
            return ServiceResult<ProjectDetailsDto>.Fail("Proje bulunamadı.", "NOT_FOUND");

        // Görüntüleme: Admin, Mentor ve Intern — tüm mevcut projeler.
        // Yönetme: Admin her zaman; Mentor yalnızca sorumlu olduğu projede.
        var canManage = viewer switch
        {
            ProjectViewerKind.Admin => true,
            ProjectViewerKind.Mentor => project.MentorUserId == actingUserId,
            _ => false
        };

        var mentorMap = await _users.GetByIdsAsync(new[] { project.MentorUserId }, cancellationToken);
        mentorMap.TryGetValue(project.MentorUserId, out var mentorInfo);

        var assignments = await (
            from a in _db.ProjectAssignments.AsNoTracking()
            join i in _db.InternProfiles.AsNoTracking() on a.InternProfileId equals i.Id
            where a.ProjectId == projectId && a.IsActive && !a.IsDeleted && !i.IsDeleted
            select new { a.InternProfileId, i.UserId, a.RoleDescription }
        ).ToListAsync(cancellationToken);

        var internUserIds = assignments.Select(a => a.UserId).Distinct().ToList();
        var internNames = await _users.GetByIdsAsync(internUserIds, cancellationToken);

        var internIds = assignments.Select(a => a.InternProfileId).Distinct().ToList();
        var periods = internIds.Count == 0
            ? []
            : await _db.InternshipPeriods.AsNoTracking()
                .Where(p => internIds.Contains(p.InternProfileId) && !p.IsDeleted)
                .OrderByDescending(p => p.StartDate)
                .Select(p => new { p.InternProfileId, p.StartDate, p.EndDate, p.Status })
                .ToListAsync(cancellationToken);

        var team = assignments.Select(a =>
        {
            internNames.TryGetValue(a.UserId, out var u);
            var period = periods.FirstOrDefault(p => p.InternProfileId == a.InternProfileId);
            return new ProjectTeamMemberDto
            {
                FullName = u?.FullName ?? "—",
                RoleDescription = a.RoleDescription,
                PeriodLabel = period is null
                    ? "Dönem yok"
                    : $"{period.StartDate:dd.MM.yyyy} – {period.EndDate:dd.MM.yyyy}",
                PeriodStatus = period?.Status
            };
        }).OrderBy(t => t.FullName).ToList();

        var tasksRaw = await _db.ProjectTasks.AsNoTracking()
            .Where(t => t.ProjectId == projectId && !t.IsDeleted)
            .OrderBy(t => t.Status).ThenBy(t => t.DueDate)
            .Select(t => new
            {
                t.Id,
                t.Title,
                t.AssignedInternProfileId,
                t.Status,
                t.Priority,
                t.DueDate,
                t.CompletedAtUtc
            })
            .ToListAsync(cancellationToken);

        var taskInternProfileIds = tasksRaw
            .Where(t => t.AssignedInternProfileId.HasValue)
            .Select(t => t.AssignedInternProfileId!.Value)
            .Distinct()
            .ToList();

        var taskInternUsers = taskInternProfileIds.Count == 0
            ? new Dictionary<Guid, Guid>()
            : await _db.InternProfiles.AsNoTracking()
                .Where(i => taskInternProfileIds.Contains(i.Id) && !i.IsDeleted)
                .ToDictionaryAsync(i => i.Id, i => i.UserId, cancellationToken);

        var taskUserIds = taskInternUsers.Values.Distinct().ToList();
        var taskNames = await _users.GetByIdsAsync(taskUserIds, cancellationToken);

        var tasks = tasksRaw.Select(t =>
        {
            string? assignee = null;
            if (t.AssignedInternProfileId.HasValue
                && taskInternUsers.TryGetValue(t.AssignedInternProfileId.Value, out var uid)
                && taskNames.TryGetValue(uid, out var info))
                assignee = info.FullName;

            return new ProjectTaskItemDto
            {
                Id = t.Id,
                Title = t.Title,
                AssignedInternFullName = assignee,
                Status = t.Status,
                Priority = t.Priority,
                DueDate = t.DueDate,
                CompletedAtUtc = t.CompletedAtUtc
            };
        }).ToList();

        // Projeye bağlı raporlar: (1) WorkItem.ProjectId (2) projeye atanmış stajyerlerin paylaşılabilir raporları
        var shareable = new[] { DailyReportStatus.Submitted, DailyReportStatus.Approved };
        var assignedInternIds = assignments.Select(a => a.InternProfileId).Distinct().ToList();

        var fromWorkItems = await (
            from w in _db.DailyWorkItems.AsNoTracking()
            join r in _db.DailyReports.AsNoTracking() on w.DailyReportId equals r.Id
            join period in _db.InternshipPeriods.AsNoTracking() on r.InternshipPeriodId equals period.Id
            join i in _db.InternProfiles.AsNoTracking() on period.InternProfileId equals i.Id
            where w.ProjectId == projectId && !w.IsDeleted && !r.IsDeleted && !period.IsDeleted && !i.IsDeleted
                  && shareable.Contains(r.Status)
            select new ReportRow(r.Id, r.ReportDate, r.Status, r.GeneralNotes, i.UserId, w.Title, w.DurationMinutes, w.TechnologiesUsed)
        ).ToListAsync(cancellationToken);

        List<ReportRow> fromAssignments = [];
        if (assignedInternIds.Count > 0)
        {
            fromAssignments = await (
                from r in _db.DailyReports.AsNoTracking()
                join period in _db.InternshipPeriods.AsNoTracking() on r.InternshipPeriodId equals period.Id
                join i in _db.InternProfiles.AsNoTracking() on period.InternProfileId equals i.Id
                where assignedInternIds.Contains(i.Id) && !r.IsDeleted && !period.IsDeleted && !i.IsDeleted
                      && shareable.Contains(r.Status)
                select new ReportRow(
                    r.Id, r.ReportDate, r.Status, r.GeneralNotes, i.UserId,
                    r.GeneralNotes ?? "Günlük çalışma", 0, null)
            ).ToListAsync(cancellationToken);

            var reportIdsNeedWork = fromAssignments.Select(x => x.Id).Distinct().ToList();
            var workForAssigned = reportIdsNeedWork.Count == 0
                ? []
                : await _db.DailyWorkItems.AsNoTracking()
                    .Where(w => reportIdsNeedWork.Contains(w.DailyReportId) && !w.IsDeleted)
                    .Select(w => new { w.DailyReportId, w.Title, w.DurationMinutes, w.TechnologiesUsed })
                    .ToListAsync(cancellationToken);

            fromAssignments = fromAssignments
                .GroupBy(x => x.Id)
                .Select(g =>
                {
                    var first = g.First();
                    var works = workForAssigned.Where(w => w.DailyReportId == first.Id).ToList();
                    if (works.Count == 0) return first;
                    return first with
                    {
                        Title = string.Join("; ", works.Select(w => w.Title)),
                        DurationMinutes = works.Sum(w => w.DurationMinutes),
                        TechnologiesUsed = string.Join(", ", works.Select(w => w.TechnologiesUsed).Where(t => !string.IsNullOrWhiteSpace(t)))
                    };
                }).ToList();
        }

        var reportRows = fromWorkItems
            .Concat(fromAssignments)
            .GroupBy(x => x.Id)
            .Select(g => g.First())
            .ToList();

        var reportUserIds = reportRows.Select(x => x.UserId).Distinct().ToList();
        var reportNames = await _users.GetByIdsAsync(reportUserIds, cancellationToken);

        var techParts = reportRows
            .Select(x => x.TechnologiesUsed)
            .Where(t => !string.IsNullOrWhiteSpace(t))
            .SelectMany(t => t!.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(12)
            .ToList();

        var linkedReports = reportRows
            .Select(first =>
            {
                reportNames.TryGetValue(first.UserId, out var u);
                var summary = !string.IsNullOrWhiteSpace(first.Title) ? first.Title : (first.GeneralNotes ?? "—");
                if (summary.Length > 280) summary = summary[..280] + "…";
                return new ProjectLinkedReportDto
                {
                    ReportId = first.Id,
                    InternFullName = u?.FullName ?? "—",
                    ReportDate = first.ReportDate,
                    WorkSummary = summary,
                    TotalDurationMinutes = first.DurationMinutes,
                    Status = first.Status
                };
            })
            .OrderByDescending(r => r.ReportDate)
            .ToList();

        var completed = tasks.Count(t => t.Status == ProjectTaskStatus.Done);
        var pending = tasks.Count(t => t.Status is ProjectTaskStatus.Todo or ProjectTaskStatus.InProgress or ProjectTaskStatus.InReview);

        var dto = new ProjectDetailsDto
        {
            Id = project.Id,
            Name = project.Name,
            Description = project.Description,
            Status = project.Status,
            StartDate = project.StartDate,
            EndDate = project.EndDate,
            CreatedAtUtc = project.CreatedAtUtc,
            OrganizationUnitName = project.OrganizationUnit?.Name ?? "—",
            MentorFullName = mentorInfo?.FullName ?? "—",
            ProgressPercentage = project.ProgressPercentage,
            RepositoryUrl = project.RepositoryUrl,
            TechnologiesSummary = techParts.Count == 0 ? null : string.Join(", ", techParts),
            CanManage = canManage,
            Team = team,
            Tasks = tasks,
            DailyReports = linkedReports,
            Summary = new ProjectSummaryDto
            {
                TotalTasks = tasks.Count,
                CompletedTasks = completed,
                PendingTasks = pending,
                TotalDailyReports = linkedReports.Count,
                TeamMemberCount = team.Count
            }
        };

        return ServiceResult<ProjectDetailsDto>.Ok(dto);
    }

    private sealed record ReportRow(
        Guid Id,
        DateOnly ReportDate,
        DailyReportStatus Status,
        string? GeneralNotes,
        Guid UserId,
        string Title,
        int DurationMinutes,
        string? TechnologiesUsed);
}
