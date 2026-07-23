using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace Staj360.Web.Areas.Manager.Models;

public class CreateTransferViewModel
{
    [Required(ErrorMessage = "Stajyer seçiniz.")]
    [Display(Name = "Stajyer")]
    public Guid InternProfileId { get; set; }

    [Required(ErrorMessage = "Hedef birim seçiniz.")]
    [Display(Name = "Hedef Şube")]
    public Guid TargetOrganizationUnitId { get; set; }

    [Display(Name = "Hedef Danışman")]
    public Guid? TargetAdvisorUserId { get; set; }

    [Display(Name = "Aynı yetki alanında doğrudan uygula")]
    public bool ExecuteImmediatelyIfSameManager { get; set; }

    [Display(Name = "Not")]
    [StringLength(1000)]
    public string? RequestNote { get; set; }

    public List<SelectListItem> Interns { get; set; } = new();
    public List<SelectListItem> Branches { get; set; } = new();
    public List<SelectListItem> Advisors { get; set; } = new();
}

public class DecideTransferViewModel
{
    public Guid TransferRequestId { get; set; }
    public bool Approve { get; set; }
    public Guid? TargetAdvisorUserId { get; set; }
    public string? DecisionNote { get; set; }
}
