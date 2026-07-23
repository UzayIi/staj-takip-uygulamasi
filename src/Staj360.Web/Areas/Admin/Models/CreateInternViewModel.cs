using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace Staj360.Web.Areas.Admin.Models;

public class CreateInternViewModel
{
    [Required(ErrorMessage = "Ad Soyad zorunludur.")]
    [Display(Name = "Ad Soyad")]
    [StringLength(150, MinimumLength = 3, ErrorMessage = "Ad Soyad en az 3 karakter olmalıdır.")]
    public string FullName { get; set; } = string.Empty;

    [Required, EmailAddress(ErrorMessage = "Geçerli bir e-posta giriniz.")]
    [Display(Name = "E-posta")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Başlangıç parolası zorunludur.")]
    [DataType(DataType.Password)]
    [Display(Name = "Başlangıç Parolası")]
    public string Password { get; set; } = string.Empty;

    [Required(ErrorMessage = "Öğrenci numarası zorunludur.")]
    [Display(Name = "Öğrenci Numarası")]
    [StringLength(50)]
    public string StudentNumber { get; set; } = string.Empty;

    [Required(ErrorMessage = "Şube seçiniz.")]
    [Display(Name = "Şube")]
    public Guid OrganizationUnitId { get; set; }

    [Required(ErrorMessage = "Danışman seçiniz.")]
    [Display(Name = "Danışman")]
    public Guid AdvisorUserId { get; set; }

    [Display(Name = "T.C. Kimlik No")]
    [StringLength(20)]
    public string? NationalId { get; set; }

    [Display(Name = "Üniversite")]
    public string? University { get; set; }

    [Display(Name = "Fakülte")]
    public string? Faculty { get; set; }

    [Display(Name = "Bölüm")]
    public string? SchoolDepartment { get; set; }

    [Display(Name = "Sınıf")]
    public string? ClassLevel { get; set; }

    [Display(Name = "Telefon")]
    [StringLength(30)]
    public string? PhoneNumber { get; set; }

    [Display(Name = "Adres")]
    [StringLength(500, ErrorMessage = "Adres en fazla 500 karakter olabilir.")]
    [DataType(DataType.MultilineText)]
    public string? Address { get; set; }

    [Display(Name = "Acil Durum Kişisi")]
    public string? EmergencyContactName { get; set; }

    [Display(Name = "Acil Durum Telefonu")]
    public string? EmergencyContactPhone { get; set; }

    /// <summary>Sunucu tarafı onay bayrağı; onay modalından sonra true gönderilir.</summary>
    public bool Confirmed { get; set; }

    public List<SelectListItem> OrganizationUnits { get; set; } = new();
    public List<SelectListItem> Advisors { get; set; } = new();
}
