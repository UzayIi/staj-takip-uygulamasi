using System.ComponentModel.DataAnnotations;

namespace Staj360.Web.Areas.Identity.Models;

public class ResetPasswordViewModel
{
    [Required]
    public string Email { get; set; } = string.Empty;

    [Required]
    public string Token { get; set; } = string.Empty;

    [Required(ErrorMessage = "Yeni parola gereklidir.")]
    [StringLength(100, ErrorMessage = "{0} en az {2} ve en fazla {1} karakter uzunluğunda olmalıdır.", MinimumLength = 8)]
    [DataType(DataType.Password)]
    [Display(Name = "Yeni Parola")]
    public string Password { get; set; } = string.Empty;

    [DataType(DataType.Password)]
    [Display(Name = "Yeni Parola (Tekrar)")]
    [Compare("Password", ErrorMessage = "Parola ve onay parolası eşleşmiyor.")]
    public string ConfirmPassword { get; set; } = string.Empty;
}
