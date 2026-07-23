using Staj360.Application.Common;
using Staj360.Domain.Entities;
using Staj360.Domain.Enums;

namespace Staj360.Application.Services.Projects;

public record CreateProjectTaskCommand(
    Guid ProjectId, Guid? AssignedInternProfileId, string Title, string? Description,
    TaskPriority Priority, DateOnly? DueDate, int? EstimatedMinutes);

public interface IProjectTaskService
{
    Task<IReadOnlyList<ProjectTask>> ListForProjectAsync(Guid projectId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ProjectTask>> ListForInternAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<ServiceResult<ProjectTask>> CreateAsync(Guid actingUserId, bool isAdmin, CreateProjectTaskCommand command, CancellationToken cancellationToken = default);
    Task<ServiceResult> UpdateStatusByInternAsync(Guid userId, Guid taskId, ProjectTaskStatus status, CancellationToken cancellationToken = default);
}
