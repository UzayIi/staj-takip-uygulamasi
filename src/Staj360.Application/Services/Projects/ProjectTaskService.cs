using Microsoft.EntityFrameworkCore;
using Staj360.Application.Abstractions;
using Staj360.Application.Common;
using Staj360.Domain.Entities;
using Staj360.Domain.Enums;

namespace Staj360.Application.Services.Projects;

public class ProjectTaskService : IProjectTaskService
{
    private readonly IApplicationDbContext _db;
    private readonly IClock _clock;
    private readonly IAuditLogService _audit;

    public ProjectTaskService(IApplicationDbContext db, IClock clock, IAuditLogService audit)
    {
        _db = db;
        _clock = clock;
        _audit = audit;
    }

    public async Task<IReadOnlyList<ProjectTask>> ListForProjectAsync(Guid projectId, CancellationToken cancellationToken = default) =>
        await _db.ProjectTasks.AsNoTracking()
            .Include(t => t.AssignedInternProfile)
            .Where(t => t.ProjectId == projectId && !t.IsDeleted)
            .OrderBy(t => t.Status).ThenByDescending(t => t.Priority)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<ProjectTask>> ListForInternAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var internProfileId = await _db.InternProfiles.AsNoTracking()
            .Where(i => i.UserId == userId && !i.IsDeleted)
            .Select(i => (Guid?)i.Id)
            .FirstOrDefaultAsync(cancellationToken);
        if (internProfileId is null)
            return Array.Empty<ProjectTask>();

        return await _db.ProjectTasks.AsNoTracking()
            .Include(t => t.Project)
            .Where(t => t.AssignedInternProfileId == internProfileId && !t.IsDeleted)
            .OrderBy(t => t.Status).ThenByDescending(t => t.Priority)
            .ToListAsync(cancellationToken);
    }

    public async Task<ServiceResult<ProjectTask>> CreateAsync(Guid actingUserId, bool isAdmin, CreateProjectTaskCommand command, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(command.Title))
            return ServiceResult<ProjectTask>.Fail("Görev başlığı zorunludur.", "VALIDATION");

        var project = await _db.Projects.FirstOrDefaultAsync(p => p.Id == command.ProjectId && !p.IsDeleted, cancellationToken);
        if (project is null)
            return ServiceResult<ProjectTask>.Fail("Proje bulunamadı.", "NOT_FOUND");

        if (!isAdmin && project.MentorUserId != actingUserId)
            return ServiceResult<ProjectTask>.Fail("Bu projede görev oluşturma yetkiniz yok.", "FORBIDDEN");

        var task = new ProjectTask
        {
            ProjectId = command.ProjectId,
            AssignedInternProfileId = command.AssignedInternProfileId,
            Title = command.Title.Trim(),
            Description = command.Description,
            Priority = command.Priority,
            Status = ProjectTaskStatus.Todo,
            DueDate = command.DueDate,
            EstimatedMinutes = command.EstimatedMinutes
        };
        _db.ProjectTasks.Add(task);
        await _db.SaveChangesAsync(cancellationToken);
        await _audit.LogAsync(nameof(ProjectTask), task.Id.ToString(), "Create", cancellationToken: cancellationToken);
        return ServiceResult<ProjectTask>.Ok(task);
    }

    public async Task<ServiceResult> UpdateStatusByInternAsync(Guid userId, Guid taskId, ProjectTaskStatus status, CancellationToken cancellationToken = default)
    {
        var task = await _db.ProjectTasks
            .Include(t => t.AssignedInternProfile)
            .FirstOrDefaultAsync(t => t.Id == taskId && !t.IsDeleted, cancellationToken);
        if (task is null)
            return ServiceResult.Fail("Görev bulunamadı.", "NOT_FOUND");

        // Stajyer yalnızca kendisine atanmış görevin durumunu güncelleyebilir.
        if (task.AssignedInternProfile?.UserId != userId)
            return ServiceResult.Fail("Yalnızca size atanmış görevlerin durumunu güncelleyebilirsiniz.", "FORBIDDEN");

        var wasDone = task.Status == ProjectTaskStatus.Done;
        task.Status = status;

        if (status == ProjectTaskStatus.Done && !wasDone)
            task.CompletedAtUtc = _clock.UtcNow;   // tamamlanınca otomatik atanır
        else if (status != ProjectTaskStatus.Done && wasDone)
            task.CompletedAtUtc = null;            // yeniden açılırsa temizlenir

        await _db.SaveChangesAsync(cancellationToken);
        await _audit.LogAsync(nameof(ProjectTask), task.Id.ToString(), $"StatusChange:{status}", cancellationToken: cancellationToken);
        return ServiceResult.Ok();
    }
}
