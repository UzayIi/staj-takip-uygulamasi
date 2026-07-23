using Microsoft.AspNetCore.Mvc.Rendering;

namespace Staj360.Web.Areas.Admin.Models;

public class AssignUnitsViewModel
{
    public Guid UserId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public HashSet<Guid> AssignedUnitIds { get; set; } = new();
    public List<SelectListItem> Branches { get; set; } = new();
}
