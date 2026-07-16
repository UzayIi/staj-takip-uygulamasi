using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Staj360.Infrastructure.Identity;
using Staj360.Web.Areas.Identity.Models;

namespace Staj360.Web.Areas.Identity.Controllers;

[Area("Identity")]
public class AccountController : Controller
{
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ILogger<AccountController> _logger;

    public AccountController(
        SignInManager<ApplicationUser> signInManager,
        UserManager<ApplicationUser> userManager,
        ILogger<AccountController> logger)
    {
        _signInManager = signInManager;
        _userManager = userManager;
        _logger = logger;
    }

    [AllowAnonymous]
    [HttpGet]
    public IActionResult Login(string? returnUrl = null)
    {
        if (User.Identity?.IsAuthenticated == true)
            return RedirectToAction("Index", "Home", new { area = "" });

        ViewData["ReturnUrl"] = returnUrl;
        return View(new LoginViewModel());
    }

    [AllowAnonymous]
    [HttpPost]
    public async Task<IActionResult> Login(LoginViewModel model, string? returnUrl = null)
    {
        ViewData["ReturnUrl"] = returnUrl;
        if (!ModelState.IsValid)
            return View(model);

        var user = await _userManager.FindByEmailAsync(model.Email);
        if (user is null)
        {
            // Kullanıcı numaralandırmasını önlemek için genel mesaj.
            ModelState.AddModelError(string.Empty, "E-posta veya parola hatalı.");
            return View(model);
        }

        if (!user.IsActive)
        {
            ModelState.AddModelError(string.Empty, "Hesabınız pasif durumda. Lütfen yöneticinizle iletişime geçin.");
            return View(model);
        }

        // lockoutOnFailure: art arda hatalı denemede geçici kilitleme.
        var result = await _signInManager.PasswordSignInAsync(user, model.Password, model.RememberMe, lockoutOnFailure: true);

        if (result.Succeeded)
        {
            _logger.LogInformation("Kullanıcı giriş yaptı: {UserId}", user.Id);
            return RedirectToLocal(returnUrl);
        }

        if (result.IsLockedOut)
        {
            ModelState.AddModelError(string.Empty, "Çok fazla hatalı deneme nedeniyle hesabınız geçici olarak kilitlendi. Lütfen daha sonra tekrar deneyin.");
            return View(model);
        }

        ModelState.AddModelError(string.Empty, "E-posta veya parola hatalı.");
        return View(model);
    }

    [Authorize]
    [HttpPost]
    public async Task<IActionResult> Logout()
    {
        await _signInManager.SignOutAsync();
        return RedirectToAction(nameof(Login));
    }

    [AllowAnonymous]
    [HttpGet]
    public IActionResult AccessDenied()
    {
        Response.StatusCode = 403;
        return View();
    }

    private IActionResult RedirectToLocal(string? returnUrl)
    {
        // Açık yönlendirme (open redirect) güvenlik açığını önle.
        if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            return Redirect(returnUrl);
        return RedirectToAction("Index", "Home", new { area = "" });
    }
}
