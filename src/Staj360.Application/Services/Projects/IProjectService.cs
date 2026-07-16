using Staj360.Application.Common;
using Staj360.Domain.Entities;
using Staj360.Domain.Enums;

namespace Staj360.Application.Services.Projects;

public record CreateProjectCommand(
    string Name, string? Description, DateOnly StartDate, DateOnly? EndDate,
    Guid DepartmentId, Guid MentorUserId, string? RepositoryUrl);

public interface IProjectService
{
    Task<PagedResult<Project>> ListAsync(int page, int pageSize, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Project>> ListForMentorAsync(Guid mentorUserId, CancellationToken cancellationToken = default);
    Task<Project?> GetAsync(Guid id, CancellationToken cancellationToken = default);
    Task<ServiceResult<Project>> CreateAsync(CreateProjectCommand command, CancellationToken cancellationToken = default);
    Task<ServiceResult> UpdateProgressAsync(Guid mentorOrAdmin, bool isAdmin, Guid projectId, int progressPercentage, ProjectStatus status, CancellationToken cancellationToken = default);
    Task<ServiceResult> AssignInternAsync(Guid actingUserId, bool isAdmin, Guid projectId, Guid internProfileId, string? roleDescription, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Project>> ListForInternAsync(Guid userId, CancellationToken cancellationToken = default);
}
