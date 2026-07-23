using Staj360.Domain.Common;

namespace Staj360.Domain.Entities;

public class Evaluation : AuditableEntity
{
    public Guid InternshipPeriodId { get; set; }
    public InternshipPeriod? InternshipPeriod { get; set; }

    public Guid MentorUserId { get; set; }
    public DateOnly EvaluationDate { get; set; }

    // Puanlar 1-5 arası (DB kısıtı ile korunur).
    public int TechnicalKnowledgeScore { get; set; }
    public int ResponsibilityScore { get; set; }
    public int TeamworkScore { get; set; }
    public int CommunicationScore { get; set; }
    public int ProblemSolvingScore { get; set; }
    public int TimeManagementScore { get; set; }
    public int AttendanceScore { get; set; }

    public string? GeneralComment { get; set; }

    public double AverageScore => (TechnicalKnowledgeScore + ResponsibilityScore + TeamworkScore +
        CommunicationScore + ProblemSolvingScore + TimeManagementScore + AttendanceScore) / 7.0;
}
