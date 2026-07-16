using Staj360.Domain.Common;
using Staj360.Domain.Enums;

namespace Staj360.Domain.Entities;

public class AiReportSummary : AuditableEntity
{
    public Guid InternshipPeriodId { get; set; }
    public InternshipPeriod? InternshipPeriod { get; set; }

    public Guid RequestedByUserId { get; set; }
    public DateOnly PeriodStart { get; set; }
    public DateOnly PeriodEnd { get; set; }
    public AiSummaryType SummaryType { get; set; }

    // Structured output alanları. Liste alanları JSON string olarak saklanır.
    public string? ExecutiveSummary { get; set; }
    public string? CompletedWork { get; set; }
    public string? Technologies { get; set; }
    public string? ProblemsAndSolutions { get; set; }
    public string? RisksOrBlockers { get; set; }
    public string? SuggestedNextSteps { get; set; }

    // Özete dahil edilen rapor Id'leri (JSON) ve girdi hash'i (idempotent cache).
    public string? SourceReportIds { get; set; }
    public string InputHash { get; set; } = string.Empty;

    public string? ModelName { get; set; }
    public string? PromptVersion { get; set; }

    public AiSummaryStatus Status { get; set; } = AiSummaryStatus.Pending;
    public string? FailureReason { get; set; }
    public DateTime? GeneratedAtUtc { get; set; }
}
