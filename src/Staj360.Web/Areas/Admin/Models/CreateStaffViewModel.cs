using System.ComponentModel.DataAnnotations;

namespace Staj360.Web.Areas.Admin.Models;

public class CreateStaffViewModel
{
    [Required(ErrorMessage = "Ad Soyad zorunludur.")]
    [Display(Name = "Ad Soyad")]
    public string FullName { get; set; } = string.Empty;

    [Required, EmailAddress(ErrorMessage = "Geçerli bir e-posta giriniz.")]
    [Display(Name = "E-posta")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Başlangıç parolası zorunludur.")]
    [DataType(DataType.Password)]
    [Display(Name = "Başlangıç Parolası")]
    public string Password { get; set; } = string.Empty;
}
