using Microsoft.EntityFrameworkCore;
using Staj360.Application.Abstractions;
using Staj360.Application.Common;
using Staj360.Domain.Entities;

namespace Staj360.Application.Services.Evaluations;

public class EvaluationService : IEvaluationService
{
    private readonly IApplicationDbContext _db;
    private readonly IAuditLogService _audit;

    public EvaluationService(IApplicationDbContext db, IAuditLogService audit)
    {
        _db = db;
        _audit = audit;
    }

    public async Task<IReadOnlyList<Evaluation>> ListForInternshipAsync(Guid internshipPeriodId, CancellationToken cancellationToken = default) =>
        await _db.Evaluations.AsNoTracking()
            .Where(e => e.InternshipPeriodId == internshipPeriodId && !e.IsDeleted)
            .OrderByDescending(e => e.EvaluationDate)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Evaluation>> ListForMentorAsync(Guid mentorUserId, CancellationToken cancellationToken = default) =>
        await _db.Evaluations.AsNoTracking()
            .Include(e => e.InternshipPeriod)!.ThenInclude(p => p!.InternProfile)
            .Where(e => e.MentorUserId == mentorUserId && !e.IsDeleted)
            .OrderByDescending(e => e.EvaluationDate)
            .ToListAsync(cancellationToken);

    public async Task<ServiceResult<Evaluation>> CreateAsync(Guid mentorUserId, CreateEvaluationCommand command, CancellationToken cancellationToken = default)
    {
        var scores = new[]
        {
            command.TechnicalKnowledgeScore, command.ResponsibilityScore, command.TeamworkScore,
            command.CommunicationScore, command.ProblemSolvingScore, command.TimeManagementScore, command.AttendanceScore
        };
        if (scores.Any(s => s is < 1 or > 5))
            return ServiceResult<Evaluation>.Fail("Tüm puanlar 1 ile 5 arasında olmalıdır.", "VALIDATION");

        var period = await _db.InternshipPeriods
            .FirstOrDefaultAsync(p => p.Id == command.InternshipPeriodId && !p.IsDeleted, cancellationToken);
        if (period is null)
            return ServiceResult<Evaluation>.Fail("Staj dönemi bulunamadı.", "NOT_FOUND");

        // Mentor yalnızca kendi stajyerini değerlendirebilir.
        if (period.MentorUserId != mentorUserId)
            return ServiceResult<Evaluation>.Fail("Bu stajyeri değerlendirme yetkiniz yok.", "FORBIDDEN");

        var entity = new Evaluation
        {
            InternshipPeriodId = command.InternshipPeriodId,
            MentorUserId = mentorUserId,
            EvaluationDate = command.EvaluationDate,
            TechnicalKnowledgeScore = command.TechnicalKnowledgeScore,
            ResponsibilityScore = command.ResponsibilityScore,
            TeamworkScore = command.TeamworkScore,
            CommunicationScore = command.CommunicationScore,
            ProblemSolvingScore = command.ProblemSolvingScore,
            TimeManagementScore = command.TimeManagementScore,
            AttendanceScore = command.AttendanceScore,
            GeneralComment = command.GeneralComment
        };
        _db.Evaluations.Add(entity);
        await _db.SaveChangesAsync(cancellationToken);
        await _audit.LogAsync(nameof(Evaluation), entity.Id.ToString(), "Create", cancellationToken: cancellationToken);
        return ServiceResult<Evaluation>.Ok(entity);
    }
}
