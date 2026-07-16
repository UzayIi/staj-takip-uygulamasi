namespace Staj360.Application.Ai.Models;

/// <summary>
/// Yapay zekânın Structured Output ile üretmesi istenen içerik şeması.
/// </summary>
public class ReportSummaryContent
{
    public string ExecutiveSummary { get; set; } = string.Empty;
    public List<string> CompletedWork { get; set; } = new();
    public List<string> Technologies { get; set; } = new();
    public List<ProblemSolution> ProblemsAndSolutions { get; set; } = new();
    public List<string> RisksOrBlockers { get; set; } = new();
    public List<string> SuggestedNextSteps { get; set; } = new();
}

public class ProblemSolution
{
    public string Problem { get; set; } = string.Empty;
    public string Solution { get; set; } = string.Empty;
}
