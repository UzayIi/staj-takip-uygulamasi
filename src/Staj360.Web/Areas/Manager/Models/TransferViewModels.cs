using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace Staj360.Web.Areas.Manager.Models;

public class CreateTransferViewModel
{
    [Required(ErrorMessage = "Stajyer seçiniz.")]
    [Display(Name = "Transfer Edilecek Stajyer")]
    public Guid InternProfileId { get; set; }

    [Display(Name = "Mevcut Daire Başkanlığı")]
    public string? SourceDirectorateName { get; set; }

    [Display(Name = "Mevcut Şube Müdürlüğü")]
    public string? SourceBranchName { get; set; }

    [Required(ErrorMessage = "Önce hedef daire başkanlığını seçiniz.")]
    [Display(Name = "Hedef Daire Başkanlığı")]
    public Guid TargetDirectorateId { get; set; }

    [Required(ErrorMessage = "Hedef şube müdürlüğü seçiniz.")]
    [Display(Name = "Hedef Şube Müdürlüğü")]
    public Guid TargetOrganizationUnitId { get; set; }

    [Display(Name = "Hedef Danışman")]
    public Guid? TargetAdvisorUserId { get; set; }

    [Display(Name = "Transfer Başlangıç Tarihi")]
    [DataType(DataType.Date)]
    public DateOnly? PlannedStartDate { get; set; }

    [Display(Name = "Aynı yetki alanında doğrudan uygula")]
    public bool ExecuteImmediatelyIfSameManager { get; set; }

    [Display(Name = "Transfer Gerekçesi")]
    [StringLength(1000)]
    public string? RequestNote { get; set; }

    public List<SelectListItem> Interns { get; set; } = new();
    public List<SelectListItem> Directorates { get; set; } = new();
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
