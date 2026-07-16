using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Staj360.Application.Common;
using Staj360.Web.Models;

namespace Staj360.Web.Controllers;

public class HomeController : Controller
{
    [Authorize]
    public IActionResult Index()
    {
        // Kullanıcıyı rolüne göre uygun panele yönlendir.
        if (User.IsInRole(AppRoles.SuperAdmin) || User.IsInRole(AppRoles.Admin))
            return RedirectToAction("Index", "Dashboard", new { area = "Admin" });
        if (User.IsInRole(AppRoles.Mentor))
            return RedirectToAction("Index", "Dashboard", new { area = "Mentor" });
        if (User.IsInRole(AppRoles.Intern))
            return RedirectToAction("Index", "Dashboard", new { area = "Intern" });

        return View();
    }

    [AllowAnonymous]
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = HttpContext.TraceIdentifier });
    }

    [AllowAnonymous]
    [ActionName("StatusCode")]
    public IActionResult HttpStatus(int code)
    {
        ViewData["Code"] = code;
        return View("StatusCode");
    }
}
