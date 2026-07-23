using Staj360.Application.Ai.Models;

namespace Staj360.Application.Ai;

/// <summary>
/// Düşük seviyeli yapay zekâ sağlayıcı sözleşmesi. Uygulamanın geri kalanından
/// bağımsız ve değiştirilebilir olması için ayrı tutulmuştur.
/// </summary>
public interface IAiProvider
{
    bool IsEnabled { get; }
    string ModelName { get; }

    Task<AiProviderResult> GenerateSummaryAsync(
        string systemPrompt,
        string userPrompt,
        CancellationToken cancellationToken = default);
}

public class AiProviderResult
{
    public bool Success { get; init; }
    public ReportSummaryContent? Content { get; init; }
    public string? FailureReason { get; init; }

    public static AiProviderResult Ok(ReportSummaryContent content) => new() { Success = true, Content = content };
    public static AiProviderResult Fail(string reason) => new() { Success = false, FailureReason = reason };
}
