using Staj360.Application.Common;
using Staj360.Domain.Entities;

namespace Staj360.Application.Ai;

/// <summary>
/// Yapay zekâ devre dışıyken (OpenAI:Enabled=false veya API anahtarı yok) kullanılır.
/// Uygulama çökmez; kullanıcıya anlaşılır mesaj döner.
/// </summary>
public class NullReportSummaryService : IReportSummaryService
{
    public bool IsEnabled => false;

    public Task<ServiceResult<AiReportSummary>> GenerateAsync(GenerateSummaryCommand command, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(ServiceResult<AiReportSummary>.Fail(
            "Yapay zekâ özeti şu anda devre dışı. Bu özelliği kullanmak için sistem yöneticisinin OpenAI ayarlarını etkinleştirmesi gerekir.",
            "AI_DISABLED"));
    }
}
