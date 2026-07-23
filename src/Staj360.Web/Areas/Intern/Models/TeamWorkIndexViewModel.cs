using Staj360.Application.Common;
using Staj360.Application.Services.TeamWork;
using Staj360.Domain.Enums;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace Staj360.Web.Areas.Intern.Models;

public class TeamWorkIndexViewModel
{
    public string Tab { get; set; } = "reports";
    public string? InternName { get; set; }
    public Guid? ProjectId { get; set; }
    public ProjectStatus? Status { get; set; }
    public DailyReportStatus? ReportStatus { get; set; }
    public DateOnly? FromDate { get; set; }
    public DateOnly? ToDate { get; set; }
    public int Page { get; set; } = 1;
    public PagedResult<TeamProjectItemDto>? Projects { get; set; }
    public PagedResult<TeamReportItemDto>? Reports { get; set; }
    public List<SelectListItem> ProjectOptions { get; set; } = new();
}
