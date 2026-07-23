using System.Text;
using Staj360.Application.Ai.Models;
using Staj360.Domain.Enums;

namespace Staj360.Application.Ai;

/// <summary>
/// Sistem talimatını ve kullanıcı istemini yalnızca güvenli rapor verilerinden üretir.
/// </summary>
public class DefaultReportSummaryPromptBuilder : IReportSummaryPromptBuilder
{
    public string PromptVersion => "v1";

    public string BuildSystemPrompt()
    {
        // Temel kurallar: yalnızca verilen raporlar, uydurma yok, kurumsal dil.
        return string.Join('\n', new[]
        {
            "Sen bir kurumun staj sürecini takip eden yardımcı bir asistansın.",
            "Görevin: sana verilen onaylı günlük çalışma raporlarını özetlemek.",
            "Kurallar:",
            "- Yanıtın tamamen Türkçe olmalı.",
            "- Yalnızca sana verilen günlük raporlardaki bilgileri kullan.",
            "- Raporlarda bulunmayan hiçbir bilgiyi uydurma.",
            "- Bir bilgi yoksa 'Raporlarda belirtilmemiştir' ifadesini kullan.",
            "- Kişilik, sağlık veya insan kaynakları kararı hakkında çıkarım yapma.",
            "- Stajyeri otomatik olarak başarılı veya başarısız ilan etme.",
            "- Kısa, kurumsal ve anlaşılır bir dil kullan.",
            "- Çalışmaları, teknolojileri, sorunları, çözümleri ve sonraki adımları ayır.",
            "İstenen alanları yapılandırılmış JSON çıktısı olarak doldur."
        });
    }

    public string BuildUserPrompt(ReportSummaryInput input)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Özet türü: {SummaryTypeText(input.SummaryType)}");
        sb.AppendLine($"Dönem: {input.PeriodStart:yyyy-MM-dd} - {input.PeriodEnd:yyyy-MM-dd}");
        sb.AppendLine($"Onaylı rapor sayısı: {input.Days.Count}");
        sb.AppendLine();
        sb.AppendLine("Günlük raporlar:");

        foreach (var day in input.Days.OrderBy(d => d.Date))
        {
            sb.AppendLine($"### Tarih: {day.Date:yyyy-MM-dd}");
            if (!string.IsNullOrWhiteSpace(day.GeneralNotes))
                sb.AppendLine($"Genel notlar: {day.GeneralNotes}");

            foreach (var item in day.WorkItems)
            {
                sb.AppendLine($"- Çalışma: {item.Title}");
                if (!string.IsNullOrWhiteSpace(item.Description))
                    sb.AppendLine($"  Açıklama: {item.Description}");
                if (!string.IsNullOrWhiteSpace(item.TechnologiesUsed))
                    sb.AppendLine($"  Teknolojiler: {item.TechnologiesUsed}");
                if (!string.IsNullOrWhiteSpace(item.Result))
                    sb.AppendLine($"  Sonuç: {item.Result}");
            }

            if (!string.IsNullOrWhiteSpace(day.ProblemsEncountered))
                sb.AppendLine($"Karşılaşılan sorunlar: {day.ProblemsEncountered}");
            if (!string.IsNullOrWhiteSpace(day.SolutionsApplied))
                sb.AppendLine($"Uygulanan çözümler: {day.SolutionsApplied}");
            if (!string.IsNullOrWhiteSpace(day.TomorrowPlan))
                sb.AppendLine($"Gelecek planı: {day.TomorrowPlan}");

            sb.AppendLine();
        }

        return sb.ToString();
    }

    private static string SummaryTypeText(AiSummaryType type) => type switch
    {
        AiSummaryType.Weekly => "Haftalık özet",
        AiSummaryType.Monthly => "Aylık özet",
        AiSummaryType.CustomRange => "Seçilen tarih aralığı özeti",
        AiSummaryType.FinalOverall => "Staj sonu genel özeti",
        _ => "Özet"
    };
}
