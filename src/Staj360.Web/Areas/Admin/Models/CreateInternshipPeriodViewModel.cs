using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace Staj360.Web.Areas.Admin.Models;

public class CreateInternshipPeriodViewModel
{
    [Required(ErrorMessage = "Stajyer seçiniz.")]
    [Display(Name = "Stajyer")]
    public Guid InternProfileId { get; set; }

    [Required(ErrorMessage = "Danışman seçiniz.")]
    [Display(Name = "Danışman")]
    public Guid MentorUserId { get; set; }

    [Required(ErrorMessage = "Çalışma programı seçiniz.")]
    [Display(Name = "Çalışma Programı")]
    public Guid WorkScheduleId { get; set; }

    [Required]
    [DataType(DataType.Date)]
    [Display(Name = "Başlangıç Tarihi")]
    public DateOnly StartDate { get; set; } = DateOnly.FromDateTime(DateTime.Today);

    [Required]
    [DataType(DataType.Date)]
    [Display(Name = "Bitiş Tarihi")]
    public DateOnly EndDate { get; set; } = DateOnly.FromDateTime(DateTime.Today.AddDays(60));

    [Range(1, 500, ErrorMessage = "Gerekli iş günü 1-500 arasında olmalıdır.")]
    [Display(Name = "Gerekli İş Günü")]
    public int RequiredWorkDays { get; set; } = 40;

    public List<SelectListItem> Interns { get; set; } = new();
    public List<SelectListItem> Mentors { get; set; } = new();
    public List<SelectListItem> Schedules { get; set; } = new();
}
