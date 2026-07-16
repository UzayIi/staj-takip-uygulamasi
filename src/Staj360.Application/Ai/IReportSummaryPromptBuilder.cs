using Staj360.Application.Ai.Models;

namespace Staj360.Application.Ai;

public interface IReportSummaryPromptBuilder
{
    string PromptVersion { get; }
    string BuildSystemPrompt();
    string BuildUserPrompt(ReportSummaryInput input);
}
