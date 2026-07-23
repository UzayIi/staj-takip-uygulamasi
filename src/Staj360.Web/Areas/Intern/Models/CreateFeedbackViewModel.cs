using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace Staj360.Web.Areas.Intern.Models;

public class CreateFeedbackViewModel
{
    [Required(ErrorMessage = "Danışman seçiniz.")]
    [Display(Name = "Danışman")]
    public Guid AdvisorUserId { get; set; }

    [Required(ErrorMessage = "Başlık zorunludur.")]
    [StringLength(250)]
    [Display(Name = "Başlık")]
    public string Title { get; set; } = string.Empty;

    [Required(ErrorMessage = "Mesaj zorunludur.")]
    [StringLength(4000)]
    [Display(Name = "Mesaj")]
    public string Message { get; set; } = string.Empty;

    public List<SelectListItem> Advisors { get; set; } = new();
}
