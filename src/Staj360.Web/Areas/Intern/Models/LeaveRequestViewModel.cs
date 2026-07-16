using System.ComponentModel.DataAnnotations;
using Staj360.Domain.Enums;

namespace Staj360.Web.Areas.Intern.Models;

public class LeaveRequestViewModel
{
    [Required(ErrorMessage = "İzin türü seçiniz.")]
    [Display(Name = "İzin Türü")]
    public LeaveType LeaveType { get; set; } = LeaveType.Excuse;

    [Required]
    [DataType(DataType.Date)]
    [Display(Name = "Başlangıç")]
    public DateOnly StartDate { get; set; } = DateOnly.FromDateTime(DateTime.Today);

    [Required]
    [DataType(DataType.Date)]
    [Display(Name = "Bitiş")]
    public DateOnly EndDate { get; set; } = DateOnly.FromDateTime(DateTime.Today);

    [Required(ErrorMessage = "Açıklama giriniz.")]
    [StringLength(1000)]
    [Display(Name = "Açıklama")]
    public string Reason { get; set; } = string.Empty;
}
