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

    public async Task<IReadOnlyList<Project>> ListForManagerUnitsAsync(
        IReadOnlyCollection<Guid> unitIds, CancellationToken cancellationToken = default)
    {
        if (unitIds.Count == 0)
            return Array.Empty<Project>();

        return await _db.Projects.AsNoTracking().Include(p => p.OrganizationUnit)
            .Where(p => !p.IsDeleted && unitIds.Contains(p.OrganizationUnitId))
            .OrderByDescending(p => p.StartDate)
            .ToListAsync(cancellationToken);
    }

    public async Task<Project?> GetAsync(Guid id, CancellationToken cancellationToken = default) =>
        await _db.Projects.Include(p => p.OrganizationUnit)
            .Include(p => p.Assignments).ThenInclude(a => a.InternProfile)
            .Include(p => p.Tasks)
            .FirstOrDefaultAsync(p => p.Id == id && !p.IsDeleted, cancellationToken);

    public async Task<ServiceResult<Project>> GetForManagerAsync(
        Guid managerId, IReadOnlyCollection<Guid> unitIds, Guid projectId, CancellationToken cancellationToken = default)
    {
        _ = managerId;
        if (unitIds.Count == 0)
            return ServiceResult<Project>.Fail("Yönetici birim ataması yok.", "FORBIDDEN");

        var project = await GetAsync(projectId, cancellationToken);
        if (project is null)
            return ServiceResult<Project>.Fail("Proje bulunamadı.", "NOT_FOUND");
        if (!unitIds.Contains(project.OrganizationUnitId))
            return ServiceResult<Project>.Fail("Bu projeyi görüntüleme yetkiniz yok.", "FORBIDDEN");

        return ServiceResult<Project>.Ok(project);
    }

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

    public Task<ServiceResult> UpdateProgressAsync(
        Guid actingUserId, bool isAdmin, Guid projectId, int progressPercentage, ProjectStatus status, CancellationToken cancellationToken = default) =>
        UpdateProgressAsync(actingUserId, isAdmin, isManager: false, managedUnitIds: null, projectId, progressPercentage, status, cancellationToken);

    public async Task<ServiceResult> UpdateProgressAsync(
        Guid actingUserId,
        bool isAdmin,
        bool isManager,
        IReadOnlyCollection<Guid>? managedUnitIds,
        Guid projectId,
        int progressPercentage,
        ProjectStatus status,
        CancellationToken cancellationToken = default)
    {
        if (progressPercentage is < 0 or > 100)
            return ServiceResult.Fail("Tamamlanma oranı 0-100 arasında olmalıdır.", "VALIDATION");

        var project = await _db.Projects.FirstOrDefaultAsync(p => p.Id == projectId && !p.IsDeleted, cancellationToken);
        if (project is null)
            return ServiceResult.Fail("Proje bulunamadı.", "NOT_FOUND");

        if (!CanManageProject(actingUserId, isAdmin, isManager, managedUnitIds, project))
            return ServiceResult.Fail("Bu projeyi yönetme yetkiniz yok.", "FORBIDDEN");

        project.ProgressPercentage = progressPercentage;
        project.Status = status;
        await _db.SaveChangesAsync(cancellationToken);
        await _audit.LogAsync(nameof(Project), project.Id.ToString(), "UpdateProgress",
            organizationUnitId: project.OrganizationUnitId, cancellationToken: cancellationToken);
        return ServiceResult.Ok();
    }

    public Task<ServiceResult> AssignInternAsync(
        Guid actingUserId, bool isAdmin, Guid projectId, Guid internProfileId, string? roleDescription, CancellationToken cancellationToken = default) =>
        AssignInternAsync(actingUserId, isAdmin, isManager: false, managedUnitIds: null, projectId, internProfileId, roleDescription, cancellationToken);

    public async Task<ServiceResult> AssignInternAsync(
        Guid actingUserId,
        bool isAdmin,
        bool isManager,
        IReadOnlyCollection<Guid>? managedUnitIds,
        Guid projectId,
        Guid internProfileId,
        string? roleDescription,
        CancellationToken cancellationToken = default)
    {
        var project = await _db.Projects.FirstOrDefaultAsync(p => p.Id == projectId && !p.IsDeleted, cancellationToken);
        if (project is null)
            return ServiceResult.Fail("Proje bulunamadı.", "NOT_FOUND");

        if (!CanManageProject(actingUserId, isAdmin, isManager, managedUnitIds, project))
            return ServiceResult.Fail("Bu projeye atama yapma yetkiniz yok.", "FORBIDDEN");

        var intern = await _db.InternProfiles.AsNoTracking()
            .FirstOrDefaultAsync(i => i.Id == internProfileId && !i.IsDeleted, cancellationToken);
        if (intern is null)
            return ServiceResult.Fail("Stajyer bulunamadı.", "NOT_FOUND");

        // Yönetici: stajyer projenin biriminde olmalı ve birim yönetici kapsamında olmalı.
        if (isManager && !isAdmin)
        {
            if (intern.CurrentOrganizationUnitId != project.OrganizationUnitId)
                return ServiceResult.Fail("Stajyer, projenin bulunduğu birimde olmalıdır.", "VALIDATION");
            if (managedUnitIds is null || !managedUnitIds.Contains(project.OrganizationUnitId))
                return ServiceResult.Fail("Bu birim yönetici kapsamınızda değil.", "FORBIDDEN");
        }

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
        await _audit.LogAsync(nameof(ProjectAssignment), projectId.ToString(), "AssignIntern",
            organizationUnitId: project.OrganizationUnitId, cancellationToken: cancellationToken);
        return ServiceResult.Ok();
    }

    public async Task<ServiceResult> EndAssignmentAsync(
        Guid actingUserId,
        bool isAdmin,
        bool isManager,
        IReadOnlyCollection<Guid>? managedUnitIds,
        Guid projectId,
        Guid assignmentId,
        DateOnly endDate,
        CancellationToken cancellationToken = default)
    {
        var project = await _db.Projects.FirstOrDefaultAsync(p => p.Id == projectId && !p.IsDeleted, cancellationToken);
        if (project is null)
            return ServiceResult.Fail("Proje bulunamadı.", "NOT_FOUND");

        if (!CanManageProject(actingUserId, isAdmin, isManager, managedUnitIds, project))
            return ServiceResult.Fail("Bu atamayı sonlandırma yetkiniz yok.", "FORBIDDEN");

        var assignment = await _db.ProjectAssignments
            .FirstOrDefaultAsync(a => a.Id == assignmentId && a.ProjectId == projectId && !a.IsDeleted, cancellationToken);
        if (assignment is null)
            return ServiceResult.Fail("Atama bulunamadı.", "NOT_FOUND");
        if (!assignment.IsActive)
            return ServiceResult.Fail("Atama zaten sonlandırılmış.", "VALIDATION");

        assignment.IsActive = false;
        await _db.SaveChangesAsync(cancellationToken);
        await _audit.LogAsync(
            nameof(ProjectAssignment),
            assignment.Id.ToString(),
            "EndAssignment",
            newValues: new { projectId, assignmentId, endDate },
            organizationUnitId: project.OrganizationUnitId,
            safeDescription: "Proje ataması sonlandırıldı.",
            cancellationToken: cancellationToken);
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

    private static bool CanManageProject(
        Guid actingUserId,
        bool isAdmin,
        bool isManager,
        IReadOnlyCollection<Guid>? managedUnitIds,
        Project project)
    {
        if (isAdmin)
            return true;
        if (project.MentorUserId == actingUserId)
            return true;
        if (isManager && managedUnitIds is not null && managedUnitIds.Contains(project.OrganizationUnitId))
            return true;
        return false;
    }
}
