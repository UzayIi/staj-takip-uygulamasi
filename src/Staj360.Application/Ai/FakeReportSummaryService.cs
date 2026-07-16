using Staj360.Application.Ai;
using Staj360.Application.Common;
using Staj360.Domain.Entities;
using Staj360.Domain.Enums;

namespace Staj360.Application.Ai;

/// <summary>
/// Testlerde gerçek OpenAI çağrısı yapmadan özet akışını doğrulamak için sahte servis.
/// </summary>
public sealed class FakeReportSummaryService : IReportSummaryService
{
    public bool IsEnabled { get; set; } = true;
    public bool ShouldFail { get; set; }
    public string FailureReason { get; set; } = "Simüle edilmiş hata";

    public Task<ServiceResult<AiReportSummary>> GenerateAsync(GenerateSummaryCommand command, CancellationToken cancellationToken = default)
    {
        if (!IsEnabled)
            return Task.FromResult(ServiceResult<AiReportSummary>.Fail("Yapay zekâ özeti devre dışı.", "AI_DISABLED"));

        if (ShouldFail)
            return Task.FromResult(ServiceResult<AiReportSummary>.Fail(FailureReason, "AI_FAILED"));

        var summary = new AiReportSummary
        {
            InternshipPeriodId = command.InternshipPeriodId,
            RequestedByUserId = command.RequestedByUserId,
            PeriodStart = command.PeriodStart,
            PeriodEnd = command.PeriodEnd,
            SummaryType = command.SummaryType,
            Status = AiSummaryStatus.Completed,
            ExecutiveSummary = "Sahte özet",
            CompletedWork = "[]",
            Technologies = "[]",
            ProblemsAndSolutions = "[]",
            RisksOrBlockers = "[]",
            SuggestedNextSteps = "[]",
            ModelName = "fake",
            PromptVersion = "v1",
            InputHash = "fake",
            GeneratedAtUtc = DateTime.UtcNow
        };

        return Task.FromResult(ServiceResult<AiReportSummary>.Ok(summary));
    }
}
