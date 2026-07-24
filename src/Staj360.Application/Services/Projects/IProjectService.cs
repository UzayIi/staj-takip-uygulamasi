using Staj360.Application.Common;
using Staj360.Domain.Entities;
using Staj360.Domain.Enums;

namespace Staj360.Application.Services.Projects;

public record CreateProjectCommand(
    string Name, string? Description, DateOnly StartDate, DateOnly? EndDate,
    Guid OrganizationUnitId, Guid MentorUserId, string? RepositoryUrl);

public interface IProjectService
{
    Task<PagedResult<Project>> ListAsync(int? year, int page, int pageSize, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<int>> GetAvailableYearsAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Project>> ListForMentorAsync(Guid mentorUserId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Project>> ListForManagerUnitsAsync(IReadOnlyCollection<Guid> unitIds, CancellationToken cancellationToken = default);
    Task<Project?> GetAsync(Guid id, CancellationToken cancellationToken = default);
    Task<ServiceResult<Project>> GetForManagerAsync(Guid managerId, IReadOnlyCollection<Guid> unitIds, Guid projectId, CancellationToken cancellationToken = default);
    Task<ServiceResult<Project>> CreateAsync(CreateProjectCommand command, CancellationToken cancellationToken = default);
    Task<ServiceResult> UpdateProgressAsync(
        Guid actingUserId,
        bool isAdmin,
        Guid projectId,
        int progressPercentage,
        ProjectStatus status,
        CancellationToken cancellationToken = default);
    Task<ServiceResult> UpdateProgressAsync(
        Guid actingUserId,
        bool isAdmin,
        bool isManager,
        IReadOnlyCollection<Guid>? managedUnitIds,
        Guid projectId,
        int progressPercentage,
        ProjectStatus status,
        CancellationToken cancellationToken = default);
    Task<ServiceResult> AssignInternAsync(
        Guid actingUserId,
        bool isAdmin,
        Guid projectId,
        Guid internProfileId,
        string? roleDescription,
        CancellationToken cancellationToken = default);
    Task<ServiceResult> AssignInternAsync(
        Guid actingUserId,
        bool isAdmin,
        bool isManager,
        IReadOnlyCollection<Guid>? managedUnitIds,
        Guid projectId,
        Guid internProfileId,
        string? roleDescription,
        CancellationToken cancellationToken = default);
    Task<ServiceResult> EndAssignmentAsync(
        Guid actingUserId,
        bool isAdmin,
        bool isManager,
        IReadOnlyCollection<Guid>? managedUnitIds,
        Guid projectId,
        Guid assignmentId,
        DateOnly endDate,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Project>> ListForInternAsync(Guid userId, CancellationToken cancellationToken = default);
}
