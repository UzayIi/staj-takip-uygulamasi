using System.ComponentModel.DataAnnotations;

namespace Staj360.Web.Areas.Intern.Models;

public class CreateDailyReportViewModel
{
    [Required]
    [DataType(DataType.Date)]
    [Display(Name = "Rapor Tarihi")]
    public DateOnly ReportDate { get; set; } = DateOnly.FromDateTime(DateTime.Today);

    [Display(Name = "Genel Notlar")]
    [StringLength(4000)]
    public string? GeneralNotes { get; set; }

    [Display(Name = "Karşılaşılan Sorunlar")]
    [StringLength(4000)]
    public string? ProblemsEncountered { get; set; }

    [Display(Name = "Uygulanan Çözümler")]
    [StringLength(4000)]
    public string? SolutionsApplied { get; set; }

    [Display(Name = "Yarınki Plan")]
    [StringLength(4000)]
    public string? TomorrowPlan { get; set; }
}

public class AddDailyWorkItemViewModel
{
    public Guid ReportId { get; set; }

    [Required(ErrorMessage = "Başlık zorunludur.")]
    [Display(Name = "Başlık")]
    [StringLength(250)]
    public string Title { get; set; } = string.Empty;

    [Display(Name = "Açıklama")]
    [StringLength(4000)]
    public string? Description { get; set; }

    [Range(1, 1440, ErrorMessage = "Süre 1-1440 dakika arasında olmalıdır.")]
    [Display(Name = "Süre (dakika)")]
    public int DurationMinutes { get; set; }

    [Display(Name = "Kullanılan Teknolojiler")]
    public string? TechnologiesUsed { get; set; }

    [Display(Name = "Sonuç")]
    public string? Result { get; set; }

    [Display(Name = "Depo Bağlantısı (URL)")]
    public string? RepositoryUrl { get; set; }
}
