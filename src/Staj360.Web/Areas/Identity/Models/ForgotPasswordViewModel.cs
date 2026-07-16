using System.ComponentModel.DataAnnotations;

namespace Staj360.Web.Areas.Identity.Models;

public class ForgotPasswordViewModel
{
    [Required(ErrorMessage = "E-posta adresi gereklidir.")]
    [EmailAddress(ErrorMessage = "Geçerli bir e-posta adresi giriniz.")]
    public string Email { get; set; } = string.Empty;
}
