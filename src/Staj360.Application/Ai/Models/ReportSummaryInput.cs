using Staj360.Domain.Enums;

namespace Staj360.Application.Ai.Models;

/// <summary>
/// Yapay zekâya gönderilecek GÜVENLİ girdi. Yalnızca onaylı raporların çalışma
/// içerikleri bulunur; kişisel/hassas veri (T.C. kimlik, telefon, e-posta, adres,
/// acil durum kişisi, IP, parola vb.) burada YER ALMAZ.
/// </summary>
public class ReportSummaryInput
{
    public DateOnly PeriodStart { get; set; }
    public DateOnly PeriodEnd { get; set; }
    public AiSummaryType SummaryType { get; set; }
    public List<ReportSummaryDayInput> Days { get; set; } = new();
}

public class ReportSummaryDayInput
{
    public DateOnly Date { get; set; }
    public string? GeneralNotes { get; set; }
    public string? ProblemsEncountered { get; set; }
    public string? SolutionsApplied { get; set; }
    public string? TomorrowPlan { get; set; }
    public List<ReportSummaryWorkItemInput> WorkItems { get; set; } = new();
}

public class ReportSummaryWorkItemInput
{
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? TechnologiesUsed { get; set; }
    public string? Result { get; set; }
}
