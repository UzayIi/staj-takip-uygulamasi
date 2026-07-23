using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Staj360.Application.Common;
using Staj360.Application.Services.Organization;

namespace Staj360.Web.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Policy = AppPolicies.AdminArea)]
public class OrganizationUnitsController : Controller
{
    private readonly IOrganizationUnitService _service;

    public OrganizationUnitsController(IOrganizationUnitService service) => _service = service;

    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var units = await _service.ListTreeAsync(cancellationToken);
        return View(units);
    }
}
