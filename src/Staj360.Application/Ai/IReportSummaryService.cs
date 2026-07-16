using Staj360.Application.Common;
using Staj360.Domain.Entities;
using Staj360.Domain.Enums;

namespace Staj360.Application.Ai;

public record GenerateSummaryCommand(
    Guid InternshipPeriodId,
    DateOnly PeriodStart,
    DateOnly PeriodEnd,
    AiSummaryType SummaryType,
    Guid RequestedByUserId);

/// <summary>
/// Yapay zekâ rapor özetleme akışının yüksek seviyeli sözleşmesi.
/// </summary>
public interface IReportSummaryService
{
    /// <summary>Yapay zekâ özelliği aktif mi? UI'da butonu buna göre gösterilir.</summary>
    bool IsEnabled { get; }

    Task<ServiceResult<AiReportSummary>> GenerateAsync(GenerateSummaryCommand command, CancellationToken cancellationToken = default);
}
