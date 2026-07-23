using Microsoft.EntityFrameworkCore;
using Staj360.Application.Abstractions;
using Staj360.Application.Common;
using Staj360.Domain.Entities;
using Staj360.Domain.Enums;

namespace Staj360.Application.Services.Projects;

public class ProjectService : IProjectService
{
    private readonly IApplicationDbContext _db;
    private readonly IClock _clock;
    private readonly IAuditLogService _audit;

    public ProjectService(IApplicationDbContext db, IClock clock, IAuditLogService audit)
    {
        _db = db;
        _clock = clock;
        _audit = audit;
    }

    public async Task<PagedResult<Project>> ListAsync(int? year, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        page = page < 1 ? 1 : page;
        pageSize = pageSize is < 1 or > 200 ? 20 : pageSize;

        var query = _db.Projects.AsNoTracking().Include(p => p.OrganizationUnit).Where(p => !p.IsDeleted);

        if (year.HasValue)
        {
            var y = year.Value;
            query = query.Where(p => p.StartDate.Year == y);
        }

        var total = await query.CountAsync(cancellationToken);
        var items = await query.OrderByDescending(p => p.StartDate).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);
        return new PagedResult<Project> { Items = items, TotalCount = total, Page = page, PageSize = pageSize };
    }

    public async Task<IReadOnlyList<int>> GetAvailableYearsAsync(CancellationToken cancellationToken = default)
    {
        var startYears = await _db.Projects.AsNoTracking()
            .Where(p => !p.IsDeleted)
            .Select(p => p.StartDate.Year)
            .Distinct()
            .ToListAsync(cancellationToken);

        var createdYears = await _db.Projects.AsNoTracking()
            .Where(p => !p.IsDeleted)
            .Select(p => p.CreatedAtUtc.Year)
            .Distinct()
            .ToListAsync(cancellationToken);

        return startYears.Union(createdYears).Distinct().OrderByDescending(y => y).ToList();
    }

    public async Task<IReadOnlyList<Project>> ListForMentorAsync(Guid mentorUserId, CancellationToken cancellationToken = default) =>
        await _db.Projects.AsNoTracking().Include(p => p.OrganizationUnit)
            .Where(p => p.MentorUserId == mentorUserId && !p.IsDeleted)
            .OrderByDescending(p => p.StartDate).ToListAsync(cancellationToken);

    public async Task<Project?> GetAsync(Guid id, CancellationToken cancellationToken = default) =>
        await _db.Projects.Include(p => p.OrganizationUnit)
            .Include(p => p.Assignments).ThenInclude(a => a.InternProfile)
            .Include(p => p.Tasks)
            .FirstOrDefaultAsync(p => p.Id == id && !p.IsDeleted, cancellationToken);

    public async Task<ServiceResult<Project>> CreateAsync(CreateProjectCommand command, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(command.Name))
            return ServiceResult<Project>.Fail("Proje adı zorunludur.", "VALIDATION");
        if (command.EndDate.HasValue && command.EndDate < command.StartDate)
            return ServiceResult<Project>.Fail("Bitiş tarihi başlangıç tarihinden önce olamaz.", "VALIDATION");

        var entity = new Project
        {
            Name = command.Name.Trim(),
            Description = command.Description,
            StartDate = command.StartDate,
            EndDate = command.EndDate,
            OrganizationUnitId = command.OrganizationUnitId,
            MentorUserId = command.MentorUserId,
            RepositoryUrl = command.RepositoryUrl,
            Status = ProjectStatus.Planned,
            ProgressPercentage = 0
        };
        _db.Projects.Add(entity);
        await _db.SaveChangesAsync(cancellationToken);
        await _audit.LogAsync(nameof(Project), entity.Id.ToString(), "Create", cancellationToken: cancellationToken);
        return ServiceResult<Project>.Ok(entity);
    }

    public async Task<ServiceResult> UpdateProgressAsync(Guid actingUserId, bool isAdmin, Guid projectId, int progressPercentage, ProjectStatus status, CancellationToken cancellationToken = default)
    {
        if (progressPercentage is < 0 or > 100)
            return ServiceResult.Fail("Tamamlanma oranı 0-100 arasında olmalıdır.", "VALIDATION");

        var project = await _db.Projects.FirstOrDefaultAsync(p => p.Id == projectId && !p.IsDeleted, cancellationToken);
        if (project is null)
            return ServiceResult.Fail("Proje bulunamadı.", "NOT_FOUND");

        // Mentor yalnızca sorumlu olduğu projeyi yönetebilir.
        if (!isAdmin && project.MentorUserId != actingUserId)
            return ServiceResult.Fail("Bu projeyi yönetme yetkiniz yok.", "FORBIDDEN");

        project.ProgressPercentage = progressPercentage;
        project.Status = status;
        await _db.SaveChangesAsync(cancellationToken);
        await _audit.LogAsync(nameof(Project), project.Id.ToString(), "UpdateProgress", cancellationToken: cancellationToken);
        return ServiceResult.Ok();
    }

    public async Task<ServiceResult> AssignInternAsync(Guid actingUserId, bool isAdmin, Guid projectId, Guid internProfileId, string? roleDescription, CancellationToken cancellationToken = default)
    {
        var project = await _db.Projects.FirstOrDefaultAsync(p => p.Id == projectId && !p.IsDeleted, cancellationToken);
        if (project is null)
            return ServiceResult.Fail("Proje bulunamadı.", "NOT_FOUND");

        if (!isAdmin && project.MentorUserId != actingUserId)
            return ServiceResult.Fail("Bu projeye atama yapma yetkiniz yok.", "FORBIDDEN");

        // Aynı stajyer aynı projeye iki kez aktif atanamaz.
        var alreadyActive = await _db.ProjectAssignments.AnyAsync(a =>
            a.ProjectId == projectId && a.InternProfileId == internProfileId && a.IsActive && !a.IsDeleted, cancellationToken);
        if (alreadyActive)
            return ServiceResult.Fail("Bu stajyer projeye zaten aktif olarak atanmış.", "DUPLICATE");

        _db.ProjectAssignments.Add(new ProjectAssignment
        {
            ProjectId = projectId,
            InternProfileId = internProfileId,
            RoleDescription = roleDescription,
            AssignedAtUtc = _clock.UtcNow,
            IsActive = true
        });
        await _db.SaveChangesAsync(cancellationToken);
        await _audit.LogAsync(nameof(ProjectAssignment), projectId.ToString(), "AssignIntern", cancellationToken: cancellationToken);
        return ServiceResult.Ok();
    }

    public async Task<IReadOnlyList<Project>> ListForInternAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var internProfileId = await _db.InternProfiles.AsNoTracking()
            .Where(i => i.UserId == userId && !i.IsDeleted)
            .Select(i => (Guid?)i.Id)
            .FirstOrDefaultAsync(cancellationToken);
        if (internProfileId is null)
            return Array.Empty<Project>();

        var projectIds = await _db.ProjectAssignments.AsNoTracking()
            .Where(a => a.InternProfileId == internProfileId && a.IsActive && !a.IsDeleted)
            .Select(a => a.ProjectId)
            .ToListAsync(cancellationToken);

        return await _db.Projects.AsNoTracking().Include(p => p.OrganizationUnit)
            .Where(p => projectIds.Contains(p.Id) && !p.IsDeleted)
            .OrderByDescending(p => p.StartDate)
            .ToListAsync(cancellationToken);
    }
}
