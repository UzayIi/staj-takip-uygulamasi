using Staj360.Application.Ai;
using Staj360.Application.Ai.Models;

namespace Staj360.UnitTests.Ai;

/// <summary>Testlerde gerçek OpenAI çağrısı yapmayan sahte sağlayıcı.</summary>
public sealed class FakeAiProvider : IAiProvider
{
    public bool IsEnabled { get; set; } = true;
    public string ModelName { get; set; } = "fake-model";
    public bool ShouldFail { get; set; }
    public string FailureReason { get; set; } = "Simüle edilmiş hata";
    public ReportSummaryContent Content { get; set; } = new()
    {
        ExecutiveSummary = "Test özeti",
        CompletedWork = ["API geliştirme"],
        Technologies = ["C#", ".NET"],
        ProblemsAndSolutions = [new ProblemSolution { Problem = "X", Solution = "Y" }],
        RisksOrBlockers = ["Raporlarda belirtilmemiştir"],
        SuggestedNextSteps = ["Testleri genişlet"]
    };

    public Task<AiProviderResult> GenerateSummaryAsync(string systemPrompt, string userPrompt, CancellationToken cancellationToken = default)
    {
        if (ShouldFail)
            return Task.FromResult(AiProviderResult.Fail(FailureReason));
        return Task.FromResult(AiProviderResult.Ok(Content));
    }
}
