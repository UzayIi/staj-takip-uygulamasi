using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Rendering;
using Staj360.Application.Ai.Models;
using Staj360.Domain.Entities;
using Staj360.Domain.Enums;

namespace Staj360.Web.Models;

public class GenerateAiSummaryViewModel
{
    [Required(ErrorMessage = "Staj dönemi seçiniz.")]
    [Display(Name = "Staj Dönemi")]
    public Guid InternshipPeriodId { get; set; }

    [Display(Name = "Özet Türü")]
    public AiSummaryType SummaryType { get; set; } = AiSummaryType.CustomRange;

    [Required]
    [DataType(DataType.Date)]
    [Display(Name = "Başlangıç")]
    public DateOnly PeriodStart { get; set; } = DateOnly.FromDateTime(DateTime.Today.AddDays(-7));

    [Required]
    [DataType(DataType.Date)]
    [Display(Name = "Bitiş")]
    public DateOnly PeriodEnd { get; set; } = DateOnly.FromDateTime(DateTime.Today);

    public bool AiEnabled { get; set; }
    public List<SelectListItem> Periods { get; set; } = new();
}

/// <summary>Kaydedilmiş özetin görüntülenmesi için ayrıştırılmış görünüm modeli.</summary>
public class AiReportSummaryViewModel
{
    public Guid Id { get; set; }
    public AiSummaryType SummaryType { get; set; }
    public AiSummaryStatus Status { get; set; }
    public DateOnly PeriodStart { get; set; }
    public DateOnly PeriodEnd { get; set; }
    public string? ModelName { get; set; }
    public DateTime? GeneratedAtUtc { get; set; }
    public string? FailureReason { get; set; }

    public string ExecutiveSummary { get; set; } = string.Empty;
    public List<string> CompletedWork { get; set; } = new();
    public List<string> Technologies { get; set; } = new();
    public List<ProblemSolution> ProblemsAndSolutions { get; set; } = new();
    public List<string> RisksOrBlockers { get; set; } = new();
    public List<string> SuggestedNextSteps { get; set; } = new();

    public static AiReportSummaryViewModel From(AiReportSummary s)
    {
        static List<T> Parse<T>(string? json) =>
            string.IsNullOrWhiteSpace(json) ? new List<T>() : (JsonSerializer.Deserialize<List<T>>(json) ?? new List<T>());

        return new AiReportSummaryViewModel
        {
            Id = s.Id,
            SummaryType = s.SummaryType,
            Status = s.Status,
            PeriodStart = s.PeriodStart,
            PeriodEnd = s.PeriodEnd,
            ModelName = s.ModelName,
            GeneratedAtUtc = s.GeneratedAtUtc,
            FailureReason = s.FailureReason,
            ExecutiveSummary = s.ExecutiveSummary ?? string.Empty,
            CompletedWork = Parse<string>(s.CompletedWork),
            Technologies = Parse<string>(s.Technologies),
            ProblemsAndSolutions = Parse<ProblemSolution>(s.ProblemsAndSolutions),
            RisksOrBlockers = Parse<string>(s.RisksOrBlockers),
            SuggestedNextSteps = Parse<string>(s.SuggestedNextSteps)
        };
    }
}
