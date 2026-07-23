using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace Staj360.Web.Areas.Admin.Models;

public class CreateTransferViewModel
{
    [Required(ErrorMessage = "Stajyer seçiniz.")]
    [Display(Name = "Stajyer")]
    public Guid InternProfileId { get; set; }

    [Required(ErrorMessage = "Hedef birim seçiniz.")]
    [Display(Name = "Hedef Şube")]
    public Guid TargetOrganizationUnitId { get; set; }

    [Required(ErrorMessage = "Hedef danışman seçiniz.")]
    [Display(Name = "Hedef Danışman")]
    public Guid TargetAdvisorUserId { get; set; }

    [Display(Name = "Not")]
    [StringLength(1000)]
    public string? RequestNote { get; set; }

    public List<SelectListItem> Interns { get; set; } = new();
    public List<SelectListItem> Branches { get; set; } = new();
    public List<SelectListItem> Advisors { get; set; } = new();
}
