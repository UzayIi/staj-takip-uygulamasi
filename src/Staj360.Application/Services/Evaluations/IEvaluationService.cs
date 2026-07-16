using Staj360.Application.Common;
using Staj360.Domain.Entities;

namespace Staj360.Application.Services.Evaluations;

public record CreateEvaluationCommand(
    Guid InternshipPeriodId, DateOnly EvaluationDate,
    int TechnicalKnowledgeScore, int ResponsibilityScore, int TeamworkScore,
    int CommunicationScore, int ProblemSolvingScore, int TimeManagementScore,
    int AttendanceScore, string? GeneralComment);

public interface IEvaluationService
{
    Task<IReadOnlyList<Evaluation>> ListForInternshipAsync(Guid internshipPeriodId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Evaluation>> ListForMentorAsync(Guid mentorUserId, CancellationToken cancellationToken = default);
    Task<ServiceResult<Evaluation>> CreateAsync(Guid mentorUserId, CreateEvaluationCommand command, CancellationToken cancellationToken = default);
}
