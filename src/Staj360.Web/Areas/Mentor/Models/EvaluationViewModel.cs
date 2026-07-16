using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace Staj360.Web.Areas.Mentor.Models;

public class CreateEvaluationViewModel
{
    [Required(ErrorMessage = "Staj dönemi seçiniz.")]
    [Display(Name = "Staj Dönemi")]
    public Guid InternshipPeriodId { get; set; }

    [DataType(DataType.Date)]
    [Display(Name = "Değerlendirme Tarihi")]
    public DateOnly EvaluationDate { get; set; } = DateOnly.FromDateTime(DateTime.Today);

    [Range(1, 5)][Display(Name = "Teknik Bilgi")] public int TechnicalKnowledgeScore { get; set; } = 3;
    [Range(1, 5)][Display(Name = "Sorumluluk")] public int ResponsibilityScore { get; set; } = 3;
    [Range(1, 5)][Display(Name = "Takım Çalışması")] public int TeamworkScore { get; set; } = 3;
    [Range(1, 5)][Display(Name = "İletişim")] public int CommunicationScore { get; set; } = 3;
    [Range(1, 5)][Display(Name = "Problem Çözme")] public int ProblemSolvingScore { get; set; } = 3;
    [Range(1, 5)][Display(Name = "Zaman Yönetimi")] public int TimeManagementScore { get; set; } = 3;
    [Range(1, 5)][Display(Name = "Devam")] public int AttendanceScore { get; set; } = 3;

    [Display(Name = "Genel Yorum")]
    [StringLength(2000)]
    public string? GeneralComment { get; set; }

    public List<SelectListItem> Periods { get; set; } = new();
}
