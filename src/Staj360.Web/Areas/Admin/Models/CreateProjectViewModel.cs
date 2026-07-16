using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace Staj360.Web.Areas.Admin.Models;

public class CreateProjectViewModel
{
    [Required(ErrorMessage = "Proje adı zorunludur.")]
    [StringLength(200)]
    [Display(Name = "Proje Adı")]
    public string Name { get; set; } = string.Empty;

    [Display(Name = "Açıklama")]
    [StringLength(2000)]
    public string? Description { get; set; }

    [Required]
    [DataType(DataType.Date)]
    [Display(Name = "Başlangıç")]
    public DateOnly StartDate { get; set; } = DateOnly.FromDateTime(DateTime.Today);

    [DataType(DataType.Date)]
    [Display(Name = "Bitiş")]
    public DateOnly? EndDate { get; set; }

    [Required(ErrorMessage = "Departman seçiniz.")]
    [Display(Name = "Departman")]
    public Guid DepartmentId { get; set; }

    [Required(ErrorMessage = "Danışman seçiniz.")]
    [Display(Name = "Danışman")]
    public Guid MentorUserId { get; set; }

    [Display(Name = "Depo URL")]
    [StringLength(500)]
    public string? RepositoryUrl { get; set; }

    public List<SelectListItem> Departments { get; set; } = new();
    public List<SelectListItem> Mentors { get; set; } = new();
}
