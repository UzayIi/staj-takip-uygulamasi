using System.Net;
using System.Net.Http;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Staj360.Application.Common;
using Staj360.Infrastructure.Identity;
using Staj360.Infrastructure.Persistence;

namespace Staj360.IntegrationTests;

public class Staj360WebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly string _dbName = Guid.NewGuid().ToString();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting(WebHostDefaults.EnvironmentKey, "Development");

        builder.ConfigureServices(services =>
        {
            // EF Core 9+: SqlServer yapılandırmasını kaldır, yoksa çift provider hatası oluşur.
            var optionsConfig = services
                .Where(d => d.ServiceType == typeof(Microsoft.EntityFrameworkCore.Infrastructure.IDbContextOptionsConfiguration<AppDbContext>))
                .ToList();
            foreach (var d in optionsConfig)
                services.Remove(d);

            services.RemoveAll(typeof(DbContextOptions<AppDbContext>));
            services.RemoveAll(typeof(AppDbContext));

            services.AddDbContext<AppDbContext>(options =>
                options.UseInMemoryDatabase(_dbName));

            services.AddScoped<Staj360.Application.Abstractions.IApplicationDbContext>(sp =>
                sp.GetRequiredService<AppDbContext>());

            // Test istemcisi HTTP kullandığı için cookie Secure=Always kırılır.
            services.PostConfigure<Microsoft.AspNetCore.Authentication.Cookies.CookieAuthenticationOptions>(
                IdentityConstants.ApplicationScheme,
                options => options.Cookie.SecurePolicy = Microsoft.AspNetCore.Http.CookieSecurePolicy.SameAsRequest);
        });
    }
}

public class AuthorizationTests : IClassFixture<Staj360WebApplicationFactory>
{
    private readonly Staj360WebApplicationFactory _factory;

    public AuthorizationTests(Staj360WebApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task Anonymous_User_IsRedirected_FromProtectedAdminPage()
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var response = await client.GetAsync("/Admin/Dashboard");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("/Identity/Account/Login", response.Headers.Location?.OriginalString, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Delete_Intern_Via_Get_Is_Rejected()
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var response = await client.GetAsync($"/Admin/Interns/Delete?id={Guid.NewGuid()}");
        Assert.True(
            response.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.MethodNotAllowed
                or HttpStatusCode.Redirect or HttpStatusCode.Found or HttpStatusCode.Unauthorized
                or HttpStatusCode.Forbidden,
            $"Unexpected: {(int)response.StatusCode}");
        Assert.NotEqual(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Anonymous_User_IsRedirected_FromProtectedMentorPage()
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var response = await client.GetAsync("/Mentor/Dashboard");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("/Identity/Account/Login", response.Headers.Location?.OriginalString, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Anonymous_User_IsRedirected_FromProtectedInternPage()
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var response = await client.GetAsync("/Intern/Dashboard");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("/Identity/Account/Login", response.Headers.Location?.OriginalString, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Intern_Cannot_Access_Admin_Interns_List()
    {
        using var scope = _factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var roles = scope.ServiceProvider.GetRequiredService<RoleManager<ApplicationRole>>();

        foreach (var role in AppRoles.All)
        {
            if (!await roles.RoleExistsAsync(role))
                await roles.CreateAsync(new ApplicationRole(role));
        }

        const string email = "intern.auth@staj360.local";
        const string password = "Intern!12345";
        if (await users.FindByEmailAsync(email) is null)
        {
            var user = new ApplicationUser
            {
                UserName = email,
                Email = email,
                FullName = "Yetki Test Stajyer",
                IsActive = true,
                EmailConfirmed = true,
                CreatedAtUtc = DateTime.UtcNow
            };
            Assert.True((await users.CreateAsync(user, password)).Succeeded);
            await users.AddToRoleAsync(user, AppRoles.Intern);
        }

        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var loginGet = await client.GetAsync("/Identity/Account/Login");
        loginGet.EnsureSuccessStatusCode();
        var token = ExtractAntiForgery(await loginGet.Content.ReadAsStringAsync());

        var form = new Dictionary<string, string>
        {
            ["Email"] = email,
            ["Password"] = password,
            ["__RequestVerificationToken"] = token
        };
        var loginPost = await client.PostAsync("/Identity/Account/Login", new FormUrlEncodedContent(form));
        Assert.True(
            loginPost.StatusCode is HttpStatusCode.Redirect or HttpStatusCode.Found or HttpStatusCode.SeeOther,
            $"Login failed: {(int)loginPost.StatusCode}");

        var response = await client.GetAsync("/Admin/Interns");
        Assert.True(
            response.StatusCode is HttpStatusCode.Redirect or HttpStatusCode.Found or HttpStatusCode.Forbidden or HttpStatusCode.Unauthorized,
            $"Unexpected status: {(int)response.StatusCode}");

        if (response.Headers.Location is not null)
        {
            var loc = response.Headers.Location.OriginalString;
            Assert.True(
                loc.Contains("AccessDenied", StringComparison.OrdinalIgnoreCase)
                || loc.Contains("Login", StringComparison.OrdinalIgnoreCase)
                || loc.Contains("Account", StringComparison.OrdinalIgnoreCase),
                loc);
        }
    }

    [Fact]
    public async Task Intern_Cannot_Download_Another_Intern_Excel()
    {
        using var scope = _factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var roles = scope.ServiceProvider.GetRequiredService<RoleManager<ApplicationRole>>();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        foreach (var role in AppRoles.All)
        {
            if (!await roles.RoleExistsAsync(role))
                await roles.CreateAsync(new ApplicationRole(role));
        }

        const string email = "intern.excel@staj360.local";
        const string password = "Intern!12345";
        if (await users.FindByEmailAsync(email) is null)
        {
            var user = new ApplicationUser
            {
                UserName = email,
                Email = email,
                FullName = "Excel Yetki Test",
                IsActive = true,
                EmailConfirmed = true,
                CreatedAtUtc = DateTime.UtcNow
            };
            Assert.True((await users.CreateAsync(user, password)).Succeeded);
            await users.AddToRoleAsync(user, AppRoles.Intern);
        }

        var unit = await db.OrganizationUnits.FirstOrDefaultAsync();
        if (unit is null)
        {
            unit = new Staj360.Domain.Entities.OrganizationUnit
            {
                Code = "BILGI_TEKNOLOJILERI",
                Name = "Bilgi Teknolojileri Şube Müdürlüğü",
                UnitType = Staj360.Domain.Enums.OrganizationUnitType.Branch,
                DisplayOrder = 10,
                IsActive = true
            };
            db.OrganizationUnits.Add(unit);
            await db.SaveChangesAsync();
        }

        var targetProfileId = Guid.NewGuid();
        if (!await db.InternProfiles.AnyAsync(p => p.StudentNumber == "AUTH-EXCEL-1"))
        {
            var targetUser = new ApplicationUser
            {
                UserName = "target.excel@staj360.local",
                Email = "target.excel@staj360.local",
                FullName = "Hedef Stajyer",
                IsActive = true,
                EmailConfirmed = true,
                CreatedAtUtc = DateTime.UtcNow
            };
            Assert.True((await users.CreateAsync(targetUser, "Target!12345")).Succeeded);
            var profile = new Staj360.Domain.Entities.InternProfile
            {
                Id = targetProfileId,
                UserId = targetUser.Id,
                StudentNumber = "AUTH-EXCEL-1",
                CurrentOrganizationUnitId = unit.Id,
                IsActive = true
            };
            db.InternProfiles.Add(profile);
            await db.SaveChangesAsync();
            targetProfileId = profile.Id;
        }
        else
        {
            targetProfileId = await db.InternProfiles
                .Where(p => p.StudentNumber == "AUTH-EXCEL-1")
                .Select(p => p.Id)
                .FirstAsync();
        }

        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var loginGet = await client.GetAsync("/Identity/Account/Login");
        loginGet.EnsureSuccessStatusCode();
        var token = ExtractAntiForgery(await loginGet.Content.ReadAsStringAsync());

        var form = new Dictionary<string, string>
        {
            ["Email"] = email,
            ["Password"] = password,
            ["__RequestVerificationToken"] = token
        };
        var loginPost = await client.PostAsync("/Identity/Account/Login", new FormUrlEncodedContent(form));
        Assert.True(
            loginPost.StatusCode is HttpStatusCode.Redirect or HttpStatusCode.Found or HttpStatusCode.SeeOther,
            $"Login failed: {(int)loginPost.StatusCode}");

        var response = await client.GetAsync($"/Admin/Interns/ExportExcel?id={targetProfileId}");
        Assert.True(
            response.StatusCode is HttpStatusCode.Redirect or HttpStatusCode.Found or HttpStatusCode.Forbidden or HttpStatusCode.Unauthorized,
            $"Unexpected status: {(int)response.StatusCode}");
        Assert.NotEqual(HttpStatusCode.OK, response.StatusCode);
    }

    private static string ExtractAntiForgery(string html)
    {
        const string marker = "name=\"__RequestVerificationToken\"";
        var idx = html.IndexOf(marker, StringComparison.Ordinal);
        Assert.True(idx >= 0, "Anti-forgery token not found");
        var valueIdx = html.IndexOf("value=\"", idx, StringComparison.Ordinal);
        Assert.True(valueIdx >= 0);
        valueIdx += "value=\"".Length;
        var end = html.IndexOf('"', valueIdx);
        return html[valueIdx..end];
    }
}
