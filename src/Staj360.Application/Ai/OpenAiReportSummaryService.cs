using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Staj360.Application.Abstractions;
using Staj360.Application.Ai.Models;
using Staj360.Application.Common;
using Staj360.Domain.Entities;
using Staj360.Domain.Enums;

namespace Staj360.Application.Ai;

/// <summary>
/// Yapay zekâ rapor özetleme akışını yönetir. Sağlayıcıdan bağımsızdır: gerçek
/// çağrıyı <see cref="IAiProvider"/> yapar (üretimde OpenAI). Yalnızca onaylı raporlar
/// kullanılır; hassas kişisel veriler prompta hiç dahil edilmez. Aynı girdi için
/// InputHash üzerinden mevcut özet yeniden kullanılır.
/// </summary>
public class OpenAiReportSummaryService : IReportSummaryService
{
    private readonly IApplicationDbContext _db;
    private readonly IAiProvider _provider;
    private readonly IReportSummaryPromptBuilder _promptBuilder;
    private readonly IClock _clock;

    public OpenAiReportSummaryService(
        IApplicationDbContext db,
        IAiProvider provider,
        IReportSummaryPromptBuilder promptBuilder,
        IClock clock)
    {
        _db = db;
        _provider = provider;
        _promptBuilder = promptBuilder;
        _clock = clock;
    }

    public bool IsEnabled => _provider.IsEnabled;

    public async Task<ServiceResult<AiReportSummary>> GenerateAsync(GenerateSummaryCommand command, CancellationToken cancellationToken = default)
    {
        if (!_provider.IsEnabled)
            return ServiceResult<AiReportSummary>.Fail("Yapay zekâ özeti şu anda devre dışı. Lütfen sistem yöneticisiyle iletişime geçin.", "AI_DISABLED");

        var periodExists = await _db.InternshipPeriods.AnyAsync(p => p.Id == command.InternshipPeriodId && !p.IsDeleted, cancellationToken);
        if (!periodExists)
            return ServiceResult<AiReportSummary>.Fail("Staj dönemi bulunamadı.", "NOT_FOUND");

        // Yalnızca ONAYLI raporlar özete dahil edilir.
        var reports = await _db.DailyReports
            .AsNoTracking()
            .Include(r => r.WorkItems)
            .Where(r => r.InternshipPeriodId == command.InternshipPeriodId &&
                        r.Status == DailyReportStatus.Approved &&
                        r.ReportDate >= command.PeriodStart &&
                        r.ReportDate <= command.PeriodEnd &&
                        !r.IsDeleted)
            .OrderBy(r => r.ReportDate)
            .ToListAsync(cancellationToken);

        if (reports.Count == 0)
            return ServiceResult<AiReportSummary>.Fail("Seçilen tarih aralığında onaylı rapor bulunamadı.", "NO_APPROVED_REPORTS");

        var input = BuildInput(command, reports);
        var inputHash = ReportSummaryHasher.Compute(input, _promptBuilder.PromptVersion, _provider.ModelName);

        // Idempotency: aynı girdi daha önce başarıyla özetlendiyse tekrar API çağrısı yapma.
        var cached = await _db.AiReportSummaries
            .FirstOrDefaultAsync(s => s.InputHash == inputHash && s.Status == AiSummaryStatus.Completed && !s.IsDeleted, cancellationToken);
        if (cached is not null)
            return ServiceResult<AiReportSummary>.Ok(cached);

        var summary = new AiReportSummary
        {
            InternshipPeriodId = command.InternshipPeriodId,
            RequestedByUserId = command.RequestedByUserId,
            PeriodStart = command.PeriodStart,
            PeriodEnd = command.PeriodEnd,
            SummaryType = command.SummaryType,
            InputHash = inputHash,
            ModelName = _provider.ModelName,
            PromptVersion = _promptBuilder.PromptVersion,
            SourceReportIds = JsonSerializer.Serialize(reports.Select(r => r.Id)),
            Status = AiSummaryStatus.Pending
        };

        var systemPrompt = _promptBuilder.BuildSystemPrompt();
        var userPrompt = _promptBuilder.BuildUserPrompt(input);

        var result = await _provider.GenerateSummaryAsync(systemPrompt, userPrompt, cancellationToken);

        if (result.Success && result.Content is not null)
        {
            summary.Status = AiSummaryStatus.Completed;
            summary.ExecutiveSummary = result.Content.ExecutiveSummary;
            summary.CompletedWork = JsonSerializer.Serialize(result.Content.CompletedWork);
            summary.Technologies = JsonSerializer.Serialize(result.Content.Technologies);
            summary.ProblemsAndSolutions = JsonSerializer.Serialize(result.Content.ProblemsAndSolutions);
            summary.RisksOrBlockers = JsonSerializer.Serialize(result.Content.RisksOrBlockers);
            summary.SuggestedNextSteps = JsonSerializer.Serialize(result.Content.SuggestedNextSteps);
            summary.GeneratedAtUtc = _clock.UtcNow;
        }
        else
        {
            summary.Status = AiSummaryStatus.Failed;
            // Güvenli, kullanıcıya gösterilebilir sebep; ham exception saklanmaz.
            summary.FailureReason = result.FailureReason ?? "Özet oluşturulamadı.";
        }

        _db.AiReportSummaries.Add(summary);
        await _db.SaveChangesAsync(cancellationToken);

        return summary.Status == AiSummaryStatus.Completed
            ? ServiceResult<AiReportSummary>.Ok(summary)
            : ServiceResult<AiReportSummary>.Fail(summary.FailureReason ?? "Özet oluşturulamadı.", "AI_FAILED");
    }

    private static ReportSummaryInput BuildInput(GenerateSummaryCommand command, List<DailyReport> reports)
    {
        // Yalnızca güvenli çalışma verileri eşlenir; kişisel/hassas alanlar EKLENMEZ.
        return new ReportSummaryInput
        {
            PeriodStart = command.PeriodStart,
            PeriodEnd = command.PeriodEnd,
            SummaryType = command.SummaryType,
            Days = reports.Select(r => new ReportSummaryDayInput
            {
                Date = r.ReportDate,
                GeneralNotes = r.GeneralNotes,
                ProblemsEncountered = r.ProblemsEncountered,
                SolutionsApplied = r.SolutionsApplied,
                TomorrowPlan = r.TomorrowPlan,
                WorkItems = r.WorkItems.Select(w => new ReportSummaryWorkItemInput
                {
                    Title = w.Title,
                    Description = w.Description,
                    TechnologiesUsed = w.TechnologiesUsed,
                    Result = w.Result
                }).ToList()
            }).ToList()
        };
    }
}
