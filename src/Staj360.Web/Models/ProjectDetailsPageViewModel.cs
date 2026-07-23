using Staj360.Application.Services.Projects;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace Staj360.Web.Models;

public class ProjectDetailsPageViewModel
{
    public ProjectDetailsDto Details { get; set; } = default!;
    public bool ShowManagePanel { get; set; }
    public string AreaName { get; set; } = string.Empty;
    public string IndexAction { get; set; } = "Index";
    public string IndexController { get; set; } = "Projects";
    public string? ReportDetailsArea { get; set; }
    public string? ReportDetailsController { get; set; }
    public string? ReportDetailsAction { get; set; }
    public List<SelectListItem> AssignableInterns { get; set; } = new();
}
