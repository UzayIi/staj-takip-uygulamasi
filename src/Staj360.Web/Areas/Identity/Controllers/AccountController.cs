using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Identity.UI.Services;
using Staj360.Application.Abstractions;
using Staj360.Infrastructure.Identity;
using Staj360.Web.Areas.Identity.Models;

namespace Staj360.Web.Areas.Identity.Controllers;

[Area("Identity")]
public class AccountController : Controller
{
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ILogger<AccountController> _logger;
    private readonly IEmailSender _emailSender;
    private readonly IAuditLogService _audit;

    public AccountController(
        SignInManager<ApplicationUser> signInManager,
        UserManager<ApplicationUser> userManager,
        ILogger<AccountController> logger,
        IEmailSender emailSender,
        IAuditLogService audit)
    {
        _signInManager = signInManager;
        _userManager = userManager;
        _logger = logger;
        _emailSender = emailSender;
        _audit = audit;
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
            await _audit.LogAsync(
                "Account", model.Email, "LoginFailed",
                safeDescription: "Giriş başarısız: kullanıcı bulunamadı.",
                isSuccessful: false,
                failureReasonCode: "USER_NOT_FOUND",
                actorNameSnapshot: model.Email);
            ModelState.AddModelError(string.Empty, "E-posta veya parola hatalı.");
            return View(model);
        }

        if (!user.IsActive)
        {
            await _audit.LogAsync(
                "Account", user.Id.ToString(), "LoginFailed",
                safeDescription: "Giriş başarısız: hesap pasif.",
                isSuccessful: false,
                failureReasonCode: "INACTIVE",
                actorUserId: user.Id,
                actorNameSnapshot: user.FullName);
            ModelState.AddModelError(string.Empty, "Hesabınız pasif durumda. Lütfen yöneticinizle iletişime geçin.");
            return View(model);
        }

        var result = await _signInManager.PasswordSignInAsync(user, model.Password, model.RememberMe, lockoutOnFailure: true);

        if (result.Succeeded)
        {
            _logger.LogInformation("Kullanıcı giriş yaptı: {UserId}", user.Id);
            var roles = await _userManager.GetRolesAsync(user);
            await _audit.LogAsync(
                "Account", user.Id.ToString(), "LoginSuccess",
                safeDescription: "Kullanıcı giriş yaptı.",
                isSuccessful: true,
                actorUserId: user.Id,
                actorNameSnapshot: user.FullName,
                actorRoleSnapshot: roles.FirstOrDefault());
            return RedirectToLocal(returnUrl);
        }

        if (result.IsLockedOut)
        {
            await _audit.LogAsync(
                "Account", user.Id.ToString(), "LoginLockout",
                safeDescription: "Hesap geçici olarak kilitlendi.",
                isSuccessful: false,
                failureReasonCode: "LOCKOUT",
                actorUserId: user.Id,
                actorNameSnapshot: user.FullName);
            ModelState.AddModelError(string.Empty, "Çok fazla hatalı deneme nedeniyle hesabınız geçici olarak kilitlendi. Lütfen daha sonra tekrar deneyin.");
            return View(model);
        }

        await _audit.LogAsync(
            "Account", user.Id.ToString(), "LoginFailed",
            safeDescription: "Giriş başarısız: hatalı parola.",
            isSuccessful: false,
            failureReasonCode: "INVALID_PASSWORD",
            actorUserId: user.Id,
            actorNameSnapshot: user.FullName);
        ModelState.AddModelError(string.Empty, "E-posta veya parola hatalı.");
        return View(model);
    }

    [Authorize]
    [HttpPost]
    public async Task<IActionResult> Logout()
    {
        var userId = _userManager.GetUserId(User);
        var user = userId is null ? null : await _userManager.FindByIdAsync(userId);
        await _signInManager.SignOutAsync();
        await _audit.LogAsync(
            "Account",
            userId ?? "unknown",
            "Logout",
            safeDescription: "Kullanıcı çıkış yaptı.",
            isSuccessful: true,
            actorUserId: user?.Id,
            actorNameSnapshot: user?.FullName);
        return RedirectToAction(nameof(Login));
    }

    [AllowAnonymous]
    [HttpGet]
    public IActionResult AccessDenied()
    {
        Response.StatusCode = 403;
        return View();
    }

    [AllowAnonymous]
    [HttpGet]
    public IActionResult ForgotPassword()
    {
        return View();
    }

    [AllowAnonymous]
    [HttpPost]
    public async Task<IActionResult> ForgotPassword(ForgotPasswordViewModel model)
    {
        if (ModelState.IsValid)
        {
            var user = await _userManager.FindByEmailAsync(model.Email);
            await _audit.LogAsync(
                "Account",
                user?.Id.ToString() ?? model.Email,
                "ForgotPasswordRequest",
                safeDescription: "Parola sıfırlama talebi alındı.",
                isSuccessful: true,
                actorUserId: user?.Id,
                actorNameSnapshot: user?.FullName ?? model.Email);

            if (user == null || !(await _userManager.IsEmailConfirmedAsync(user)))
            {
                return RedirectToAction(nameof(ForgotPasswordConfirmation));
            }

            var code = await _userManager.GeneratePasswordResetTokenAsync(user);
            var callbackUrl = Url.Action(
                nameof(ResetPassword),
                "Account",
                new { area = "Identity", code },
                protocol: Request.Scheme);

            await _emailSender.SendEmailAsync(
                model.Email,
                "Şifre Sıfırlama",
                $"Şifrenizi sıfırlamak için lütfen <a href='{callbackUrl}'>buraya tıklayın</a>.");

            return RedirectToAction(nameof(ForgotPasswordConfirmation));
        }

        return View(model);
    }

    [AllowAnonymous]
    [HttpGet]
    public IActionResult ForgotPasswordConfirmation()
    {
        return View();
    }

    [AllowAnonymous]
    [HttpGet]
    public IActionResult ResetPassword(string? code = null)
    {
        if (code == null)
        {
            return BadRequest("Şifre sıfırlama için kod sağlanmalıdır.");
        }
        else
        {
            return View(new ResetPasswordViewModel { Token = code });
        }
    }

    [AllowAnonymous]
    [HttpPost]
    public async Task<IActionResult> ResetPassword(ResetPasswordViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var user = await _userManager.FindByEmailAsync(model.Email);
        if (user == null)
        {
            return RedirectToAction(nameof(ResetPasswordConfirmation));
        }

        var result = await _userManager.ResetPasswordAsync(user, model.Token, model.Password);
        if (result.Succeeded)
        {
            return RedirectToAction(nameof(ResetPasswordConfirmation));
        }

        foreach (var error in result.Errors)
        {
            ModelState.AddModelError(string.Empty, error.Description);
        }
        return View(model);
    }

    [AllowAnonymous]
    [HttpGet]
    public IActionResult ResetPasswordConfirmation()
    {
        return View();
    }

    private IActionResult RedirectToLocal(string? returnUrl)
    {
        if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            return Redirect(returnUrl);
        return RedirectToAction("Index", "Home", new { area = "" });
    }
}
