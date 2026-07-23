using System.Security.Cryptography;
using System.Text;
using Staj360.Application.Ai.Models;

namespace Staj360.Application.Ai;

/// <summary>
/// Aynı raporlar değişmeden tekrar özetlenmek istenirse mevcut özetin kullanılabilmesi
/// için kararlı (deterministik) bir girdi hash'i üretir.
/// </summary>
public static class ReportSummaryHasher
{
    public static string Compute(ReportSummaryInput input, string promptVersion, string modelName)
    {
        var sb = new StringBuilder();
        sb.Append(promptVersion).Append('|').Append(modelName).Append('|');
        sb.Append(input.SummaryType).Append('|');
        sb.Append(input.PeriodStart.ToString("yyyy-MM-dd")).Append('|');
        sb.Append(input.PeriodEnd.ToString("yyyy-MM-dd")).Append('|');

        // Tarihe göre sıralayarak kararlılığı garanti et.
        foreach (var day in input.Days.OrderBy(d => d.Date))
        {
            sb.Append(day.Date.ToString("yyyy-MM-dd")).Append(';');
            sb.Append(day.GeneralNotes).Append(';');
            sb.Append(day.ProblemsEncountered).Append(';');
            sb.Append(day.SolutionsApplied).Append(';');
            sb.Append(day.TomorrowPlan).Append(';');
            foreach (var item in day.WorkItems.OrderBy(w => w.Title, StringComparer.Ordinal))
            {
                sb.Append(item.Title).Append(',');
                sb.Append(item.Description).Append(',');
                sb.Append(item.TechnologiesUsed).Append(',');
                sb.Append(item.Result).Append('#');
            }
        }

        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(sb.ToString()));
        return Convert.ToHexString(bytes);
    }
}
