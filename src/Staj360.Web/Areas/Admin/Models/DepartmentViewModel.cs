using System.ComponentModel.DataAnnotations;

namespace Staj360.Web.Areas.Admin.Models;

public class DepartmentViewModel
{
    public Guid? Id { get; set; }

    [Required(ErrorMessage = "Departman adı zorunludur.")]
    [StringLength(150)]
    [Display(Name = "Departman Adı")]
    public string Name { get; set; } = string.Empty;

    [StringLength(1000)]
    [Display(Name = "Açıklama")]
    public string? Description { get; set; }

    [Display(Name = "Aktif")]
    public bool IsActive { get; set; } = true;
}
