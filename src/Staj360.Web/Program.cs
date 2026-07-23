using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Staj360.Application;
using Staj360.Application.Abstractions;
using Staj360.Application.Common;
using Staj360.Infrastructure;
using Staj360.Infrastructure.Persistence;
using Staj360.Web.Services;

var builder = WebApplication.CreateBuilder(args);

// Katman servisleri
builder.Services.AddApplication();
builder.Services.AddInfrastructure(
    builder.Configuration,
    Path.Combine(builder.Environment.ContentRootPath, "uploads"),
    builder.Environment.IsDevelopment());

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUserService, HttpCurrentUserService>();

// MVC + global anti-forgery (tüm POST'larda CSRF koruması)
builder.Services.AddControllersWithViews(options =>
{
    options.Filters.Add(new AutoValidateAntiforgeryTokenAttribute());
});

// Güvenli cookie ve yönlendirme ayarları
builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Identity/Account/Login";
    options.LogoutPath = "/Identity/Account/Logout";
    options.AccessDeniedPath = "/Identity/Account/AccessDenied";
    options.ExpireTimeSpan = TimeSpan.FromHours(8);
    options.SlidingExpiration = true;
    options.Cookie.HttpOnly = true;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    options.Cookie.SameSite = SameSiteMode.Lax;
});

// Rol/policy tabanlı yetkilendirme
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy(AppPolicies.AdminArea, p => p.RequireRole(AppRoles.SuperAdmin, AppRoles.Admin));
    options.AddPolicy(AppPolicies.ManagerArea, p => p.RequireRole(AppRoles.Manager));
    options.AddPolicy(AppPolicies.MentorArea, p => p.RequireRole(AppRoles.Mentor));
    options.AddPolicy(AppPolicies.InternArea, p => p.RequireRole(AppRoles.Intern));
    options.AddPolicy(AppPolicies.StaffOnly, p => p.RequireRole(AppRoles.SuperAdmin, AppRoles.Admin, AppRoles.Manager, AppRoles.Mentor));
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}
else
{
    // Production'da ayrıntılı hata gösterme.
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

// Yetkisiz (403) ve bulunamadı (404) için düzgün sayfa yönlendirmesi
app.UseStatusCodePagesWithReExecute("/Home/StatusCode", "?code={0}");

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "areas",
    pattern: "{area:exists}/{controller=Home}/{action=Index}/{id?}");

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

// Veritabanı migration + seed
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        var db = services.GetRequiredService<AppDbContext>();
        if (db.Database.IsRelational())
            await db.Database.MigrateAsync();
        await DbSeeder.SeedAsync(services, app.Environment.IsDevelopment());
    }
    catch (Exception ex)
    {
        var logger = services.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "Veritabanı başlatma/seed sırasında hata oluştu.");
    }
}

app.Run();

// Integration test projesinden erişim için.
public partial class Program { }
