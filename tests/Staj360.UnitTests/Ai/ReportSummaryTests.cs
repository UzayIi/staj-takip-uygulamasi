using Microsoft.EntityFrameworkCore;
using Staj360.Application.Ai;
using Staj360.Application.Ai.Models;
using Staj360.Application.Common;
using Staj360.Domain.Entities;
using Staj360.Domain.Enums;
using Staj360.UnitTests.TestSupport;

namespace Staj360.UnitTests.Ai;

public class ReportSummaryTests
{
    [Fact]
    public void InputHash_IsStable_ForSameInput()
    {
        var input = SampleInput();
        var a = ReportSummaryHasher.Compute(input, "v1", "model-a");
        var b = ReportSummaryHasher.Compute(input, "v1", "model-a");
        Assert.Equal(a, b);
    }

    [Fact]
    public void InputHash_Changes_WhenContentChanges()
    {
        var input = SampleInput();
        var a = ReportSummaryHasher.Compute(input, "v1", "model-a");
        input.Days[0].GeneralNotes = "değişti";
        var b = ReportSummaryHasher.Compute(input, "v1", "model-a");
        Assert.NotEqual(a, b);
    }

    [Fact]
    public void PromptBuilder_DoesNotInclude_SensitivePersonalData()
    {
        var builder = new DefaultReportSummaryPromptBuilder();
        var prompt = builder.BuildUserPrompt(SampleInput());
        var system = builder.BuildSystemPrompt();

        Assert.DoesNotContain("12345678901", prompt);
        Assert.DoesNotContain("5551112233", prompt);
        Assert.DoesNotContain("@", prompt); // e-posta örneği yok
        Assert.DoesNotContain("T.C.", system);
        Assert.Contains("API geliştirme", prompt);
        Assert.Contains("C#", prompt);
    }

    [Fact]
    public async Task NullReportSummaryService_DoesNotThrow_WhenDisabled()
    {
        var service = new NullReportSummaryService();
        Assert.False(service.IsEnabled);

        var result = await service.GenerateAsync(new GenerateSummaryCommand(
            Guid.NewGuid(), new DateOnly(2026, 7, 1), new DateOnly(2026, 7, 7), AiSummaryType.Weekly, Guid.NewGuid()));

        Assert.False(result.Success);
        Assert.Equal("AI_DISABLED", result.ErrorCode);
    }

    [Fact]
    public async Task FakeProvider_StructuredResponse_IsPersisted()
    {
        var utc = new DateTime(2026, 7, 13, 10, 0, 0, DateTimeKind.Utc);
        var (db, _, mentorId, periodId, _) = await TestDbFactory.SeedActiveInternAsync(utc, new TimeOnly(9, 0));
        try
        {
            await SeedApprovedReportAsync(db, periodId, new DateOnly(2026, 7, 13));

            var provider = new FakeAiProvider();
            var service = new OpenAiReportSummaryService(db, provider, new DefaultReportSummaryPromptBuilder(), new TestClock(utc));

            var result = await service.GenerateAsync(new GenerateSummaryCommand(
                periodId, new DateOnly(2026, 7, 1), new DateOnly(2026, 7, 31), AiSummaryType.Monthly, mentorId));

            Assert.True(result.Success);
            Assert.Equal(AiSummaryStatus.Completed, result.Data!.Status);
            Assert.Equal("Test özeti", result.Data.ExecutiveSummary);
            Assert.Contains("API geli", result.Data.CompletedWork, StringComparison.Ordinal);
        }
        finally { await TestDbFactory.DisposeDatabaseAsync(db); }
    }

    [Fact]
    public async Task Generate_OnlyIncludes_ApprovedReports()
    {
        var utc = new DateTime(2026, 7, 13, 10, 0, 0, DateTimeKind.Utc);
        var (db, _, mentorId, periodId, _) = await TestDbFactory.SeedActiveInternAsync(utc, new TimeOnly(9, 0));
        try
        {
            var unitId = await db.InternshipPeriods.AsNoTracking()
                .Where(p => p.Id == periodId)
                .Select(p => p.InternProfile!.CurrentOrganizationUnitId)
                .FirstAsync();
            db.DailyReports.Add(new DailyReport
            {
                InternshipPeriodId = periodId,
                ReportDate = new DateOnly(2026, 7, 12),
                OrganizationUnitId = unitId,
                Status = DailyReportStatus.Submitted,
                GeneralNotes = "henüz onaylı değil",
                WorkItems = { new DailyWorkItem { Title = "Taslak iş", DurationMinutes = 30 } }
            });
            await db.SaveChangesAsync();

            var provider = new FakeAiProvider();
            var service = new OpenAiReportSummaryService(db, provider, new DefaultReportSummaryPromptBuilder(), new TestClock(utc));

            var result = await service.GenerateAsync(new GenerateSummaryCommand(
                periodId, new DateOnly(2026, 7, 1), new DateOnly(2026, 7, 31), AiSummaryType.CustomRange, mentorId));

            Assert.False(result.Success);
            Assert.Equal("NO_APPROVED_REPORTS", result.ErrorCode);
        }
        finally { await TestDbFactory.DisposeDatabaseAsync(db); }
    }

    [Fact]
    public async Task FailedAiCall_SetsSafeFailedStatus()
    {
        var utc = new DateTime(2026, 7, 13, 10, 0, 0, DateTimeKind.Utc);
        var (db, _, mentorId, periodId, _) = await TestDbFactory.SeedActiveInternAsync(utc, new TimeOnly(9, 0));
        try
        {
            await SeedApprovedReportAsync(db, periodId, new DateOnly(2026, 7, 13));

            var provider = new FakeAiProvider { ShouldFail = true, FailureReason = "Geçici sağlayıcı hatası" };
            var service = new OpenAiReportSummaryService(db, provider, new DefaultReportSummaryPromptBuilder(), new TestClock(utc));

            var result = await service.GenerateAsync(new GenerateSummaryCommand(
                periodId, new DateOnly(2026, 7, 1), new DateOnly(2026, 7, 31), AiSummaryType.Weekly, mentorId));

            Assert.False(result.Success);
            Assert.Equal("AI_FAILED", result.ErrorCode);
            Assert.Contains("Geçici", result.ErrorMessage);
            Assert.DoesNotContain("Exception", result.ErrorMessage ?? string.Empty);
        }
        finally { await TestDbFactory.DisposeDatabaseAsync(db); }
    }

    private static async Task SeedApprovedReportAsync(Staj360.Infrastructure.Persistence.AppDbContext db, Guid periodId, DateOnly date)
    {
        var unitId = await db.InternshipPeriods.AsNoTracking()
            .Where(p => p.Id == periodId)
            .Select(p => p.InternProfile!.CurrentOrganizationUnitId)
            .FirstAsync();

        db.DailyReports.Add(new DailyReport
        {
            InternshipPeriodId = periodId,
            ReportDate = date,
            OrganizationUnitId = unitId,
            Status = DailyReportStatus.Approved,
            GeneralNotes = "API geliştirme tamamlandı",
            ProblemsEncountered = "Bağımlılık çakışması",
            SolutionsApplied = "Sürüm yükseltildi",
            TomorrowPlan = "Test yazılacak",
            WorkItems =
            {
                new DailyWorkItem
                {
                    Title = "API geliştirme",
                    Description = "REST uçları",
                    TechnologiesUsed = "C#",
                    Result = "Tamamlandı",
                    DurationMinutes = 120
                }
            }
        });
        await db.SaveChangesAsync();
    }

    private static ReportSummaryInput SampleInput() => new()
    {
        PeriodStart = new DateOnly(2026, 7, 1),
        PeriodEnd = new DateOnly(2026, 7, 7),
        SummaryType = AiSummaryType.Weekly,
        Days =
        [
            new ReportSummaryDayInput
            {
                Date = new DateOnly(2026, 7, 1),
                GeneralNotes = "API geliştirme",
                WorkItems =
                [
                    new ReportSummaryWorkItemInput
                    {
                        Title = "API geliştirme",
                        Description = "Endpoint",
                        TechnologiesUsed = "C#",
                        Result = "OK"
                    }
                ]
            }
        ]
    };
}
