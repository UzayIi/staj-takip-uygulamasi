using Microsoft.EntityFrameworkCore;
using Staj360.Application.Common;
using Staj360.Application.Services.TeamWork;
using Staj360.Domain.Enums;
using Staj360.Infrastructure.Persistence;

namespace Staj360.Infrastructure.Services;

public class TeamWorkService : ITeamWorkService
{
    private static readonly DailyReportStatus[] PeerVisibleStatuses =
    [
        DailyReportStatus.Submitted,
        DailyReportStatus.Approved
    ];

    private readonly AppDbContext _db;

    public TeamWorkService(AppDbContext db) => _db = db;

    public async Task<PagedResult<TeamProjectItemDto>> ListPeerProjectsAsync(
        TeamProjectFilter filter, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        page = page < 1 ? 1 : page;
        pageSize = pageSize is < 1 or > 100 ? 20 : pageSize;

        var query =
            from a in _db.ProjectAssignments.AsNoTracking()
            join p in _db.Projects.AsNoTracking() on a.ProjectId equals p.Id
            join i in _db.InternProfiles.AsNoTracking() on a.InternProfileId equals i.Id
            join u in _db.Users.AsNoTracking() on i.UserId equals u.Id
            where !a.IsDeleted && a.IsActive && !p.IsDeleted && !i.IsDeleted
            select new { Assignment = a, Project = p, User = u };

        if (!string.IsNullOrWhiteSpace(filter.InternName))
        {
            var term = filter.InternName.Trim();
            query = query.Where(x => x.User.FullName.Contains(term));
        }

        if (filter.Status.HasValue)
            query = query.Where(x => x.Project.Status == filter.Status.Value);

        if (filter.FromDate.HasValue)
            query = query.Where(x => x.Project.StartDate >= filter.FromDate.Value);

        if (filter.ToDate.HasValue)
            query = query.Where(x => x.Project.StartDate <= filter.ToDate.Value);

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(x => x.Project.StartDate)
            .ThenBy(x => x.User.FullName)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new TeamProjectItemDto
            {
                ProjectId = x.Project.Id,
                InternFullName = x.User.FullName,
                ProjectName = x.Project.Name,
                ProjectDescription = x.Project.Description,
                Status = x.Project.Status,
                StartDate = x.Project.StartDate,
                EndDate = x.Project.EndDate,
                RoleDescription = x.Assignment.RoleDescription
            })
            .ToListAsync(cancellationToken);

        return new PagedResult<TeamProjectItemDto>
        {
            Items = items,
            TotalCount = total,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<PagedResult<TeamReportItemDto>> ListPeerReportsAsync(
        TeamReportFilter filter, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        page = page < 1 ? 1 : page;
        pageSize = pageSize is < 1 or > 100 ? 20 : pageSize;

        var query =
            from r in _db.DailyReports.AsNoTracking()
            join period in _db.InternshipPeriods.AsNoTracking() on r.InternshipPeriodId equals period.Id
            join i in _db.InternProfiles.AsNoTracking() on period.InternProfileId equals i.Id
            join u in _db.Users.AsNoTracking() on i.UserId equals u.Id
            where !r.IsDeleted && !period.IsDeleted && !i.IsDeleted
                  && PeerVisibleStatuses.Contains(r.Status)
            select new { Report = r, User = u, InternProfileId = i.Id };

        if (!string.IsNullOrWhiteSpace(filter.InternName))
        {
            var term = filter.InternName.Trim();
            query = query.Where(x => x.User.FullName.Contains(term));
        }

        if (filter.FromDate.HasValue)
            query = query.Where(x => x.Report.ReportDate >= filter.FromDate.Value);

        if (filter.ToDate.HasValue)
            query = query.Where(x => x.Report.ReportDate <= filter.ToDate.Value);

        if (filter.Status.HasValue)
        {
            if (!PeerVisibleStatuses.Contains(filter.Status.Value))
            {
                return new PagedResult<TeamReportItemDto>
                {
                    Items = Array.Empty<TeamReportItemDto>(),
                    TotalCount = 0,
                    Page = page,
                    PageSize = pageSize
                };
            }
            query = query.Where(x => x.Report.Status == filter.Status.Value);
        }

        if (filter.ProjectId.HasValue)
        {
            var projectId = filter.ProjectId.Value;
            var reportIdsForProject = _db.DailyWorkItems.AsNoTracking()
                .Where(w => w.ProjectId == projectId && !w.IsDeleted)
                .Select(w => w.DailyReportId);

            var internIdsOnProject = _db.ProjectAssignments.AsNoTracking()
                .Where(a => a.ProjectId == projectId && a.IsActive && !a.IsDeleted)
                .Select(a => a.InternProfileId);

            query = query.Where(x =>
                reportIdsForProject.Contains(x.Report.Id) || internIdsOnProject.Contains(x.InternProfileId));
        }

        var total = await query.CountAsync(cancellationToken);

        var pageRows = await query
            .OrderByDescending(x => x.Report.ReportDate)
            .ThenBy(x => x.User.FullName)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new
            {
                x.Report.Id,
                x.User.FullName,
                x.Report.ReportDate,
                x.Report.Status,
                x.Report.GeneralNotes
            })
            .ToListAsync(cancellationToken);

        var reportIds = pageRows.Select(r => r.Id).ToList();
        var workItems = reportIds.Count == 0
            ? []
            : await (
                from w in _db.DailyWorkItems.AsNoTracking()
                join p in _db.Projects.AsNoTracking() on w.ProjectId equals p.Id into pj
                from p in pj.DefaultIfEmpty()
                where reportIds.Contains(w.DailyReportId) && !w.IsDeleted
                select new
                {
                    w.DailyReportId,
                    w.Title,
                    w.DurationMinutes,
                    w.TechnologiesUsed,
                    ProjectName = p != null ? p.Name : null
                }
            ).ToListAsync(cancellationToken);

        var workByReport = workItems
            .GroupBy(w => w.DailyReportId)
            .ToDictionary(
                g => g.Key,
                g => (
                    Minutes: g.Sum(x => x.DurationMinutes),
                    Titles: string.Join("; ", g.Select(x => x.Title)),
                    Tech: string.Join(", ", g.Select(x => x.TechnologiesUsed).Where(t => !string.IsNullOrWhiteSpace(t)).Distinct()),
                    Project: g.Select(x => x.ProjectName).FirstOrDefault(n => !string.IsNullOrWhiteSpace(n))
                ));

        var items = pageRows.Select(r =>
        {
            workByReport.TryGetValue(r.Id, out var work);
            var summary = !string.IsNullOrWhiteSpace(work.Titles)
                ? work.Titles
                : (r.GeneralNotes ?? "—");
            return new TeamReportItemDto
            {
                ReportId = r.Id,
                InternFullName = r.FullName,
                ReportDate = r.ReportDate,
                ProjectName = work.Project,
                WorkSummary = summary.Length > 300 ? summary[..300] + "…" : summary,
                TotalDurationMinutes = work.Minutes,
                TechnologiesUsed = string.IsNullOrWhiteSpace(work.Tech) ? null : work.Tech,
                Status = r.Status
            };
        }).ToList();

        return new PagedResult<TeamReportItemDto>
        {
            Items = items,
            TotalCount = total,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<ServiceResult<TeamReportDetailDto>> GetPeerReportDetailAsync(
        Guid reportId, Guid viewerUserId, CancellationToken cancellationToken = default)
    {
        var row = await (
            from r in _db.DailyReports.AsNoTracking()
            join period in _db.InternshipPeriods.AsNoTracking() on r.InternshipPeriodId equals period.Id
            join i in _db.InternProfiles.AsNoTracking() on period.InternProfileId equals i.Id
            join u in _db.Users.AsNoTracking() on i.UserId equals u.Id
            where r.Id == reportId && !r.IsDeleted && !period.IsDeleted && !i.IsDeleted
            select new
            {
                Report = r,
                OwnerUserId = i.UserId,
                OwnerName = u.FullName
            }
        ).FirstOrDefaultAsync(cancellationToken);

        if (row is null)
            return ServiceResult<TeamReportDetailDto>.Fail("Rapor bulunamadı.", "NOT_FOUND");

        var isOwner = row.OwnerUserId == viewerUserId;
        var status = row.Report.Status;

        if (status == DailyReportStatus.Draft && !isOwner)
            return ServiceResult<TeamReportDetailDto>.Fail("Rapor bulunamadı.", "NOT_FOUND");

        if (status == DailyReportStatus.Rejected && !isOwner)
            return ServiceResult<TeamReportDetailDto>.Fail("Rapor bulunamadı.", "NOT_FOUND");

        if (!isOwner && !PeerVisibleStatuses.Contains(status))
            return ServiceResult<TeamReportDetailDto>.Fail("Rapor bulunamadı.", "NOT_FOUND");

        var workItems = await (
            from w in _db.DailyWorkItems.AsNoTracking()
            join p in _db.Projects.AsNoTracking() on w.ProjectId equals p.Id into pj
            from p in pj.DefaultIfEmpty()
            where w.DailyReportId == reportId && !w.IsDeleted
            select new TeamReportWorkItemDto
            {
                Title = w.Title,
                Description = w.Description,
                DurationMinutes = w.DurationMinutes,
                TechnologiesUsed = w.TechnologiesUsed,
                ProjectName = p != null ? p.Name : null
            }
        ).ToListAsync(cancellationToken);

        var projectName = workItems.Select(w => w.ProjectName).FirstOrDefault(n => !string.IsNullOrWhiteSpace(n));
        var tech = string.Join(", ", workItems
            .Select(w => w.TechnologiesUsed)
            .Where(t => !string.IsNullOrWhiteSpace(t))
            .Distinct());
        var workSummary = workItems.Count > 0
            ? string.Join("; ", workItems.Select(w => w.Title))
            : (row.Report.GeneralNotes ?? "—");

        var dto = new TeamReportDetailDto
        {
            ReportId = row.Report.Id,
            InternFullName = row.OwnerName,
            ReportDate = row.Report.ReportDate,
            ProjectName = projectName,
            WorkSummary = workSummary,
            // Mentor özel notları asla paylaşılmaz.
            ProblemsEncountered = isOwner || PeerVisibleStatuses.Contains(status) ? row.Report.ProblemsEncountered : null,
            SolutionsApplied = isOwner || PeerVisibleStatuses.Contains(status) ? row.Report.SolutionsApplied : null,
            TotalDurationMinutes = workItems.Sum(w => w.DurationMinutes),
            TechnologiesUsed = string.IsNullOrWhiteSpace(tech) ? null : tech,
            Status = status,
            WorkItems = workItems
        };

        return ServiceResult<TeamReportDetailDto>.Ok(dto);
    }

    public async Task<IReadOnlyList<(Guid Id, string Name)>> ListProjectsForFilterAsync(
        CancellationToken cancellationToken = default) =>
        await _db.Projects.AsNoTracking()
            .Where(p => !p.IsDeleted)
            .OrderBy(p => p.Name)
            .Select(p => new ValueTuple<Guid, string>(p.Id, p.Name))
            .ToListAsync(cancellationToken);
}
