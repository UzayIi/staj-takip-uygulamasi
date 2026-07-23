using ClosedXML.Excel;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Staj360.Application.Abstractions;
using Staj360.Application.Common;
using Staj360.Application.Services.Exports;
using Staj360.Domain.Entities;
using Staj360.Domain.Enums;
using Staj360.Infrastructure.Configuration;
using Staj360.Infrastructure.Identity;
using Staj360.Infrastructure.Persistence;
using Staj360.Infrastructure.Services;
using Staj360.UnitTests.TestSupport;

namespace Staj360.UnitTests.Exports;

public class InternExcelExportTests
{
    [Fact]
    public async Task ExportAsync_Returns_Xlsx_With_Expected_Sheets_And_FileName()
    {
        var utc = new DateTime(2026, 7, 16, 10, 0, 0, DateTimeKind.Utc);
        await using var sp = await CreateHostAsync(utc);
        try
        {
            var db = sp.GetRequiredService<AppDbContext>();
            var users = sp.GetRequiredService<UserManager<ApplicationUser>>();
            var export = sp.GetRequiredService<IInternExcelExportService>();

            var user = new ApplicationUser
            {
                UserName = "excel.stajyer@test.local",
                Email = "excel.stajyer@test.local",
                FullName = "Ayşe Yılmaz",
                IsActive = true,
                EmailConfirmed = true,
                CreatedAtUtc = utc
            };
            Assert.True((await users.CreateAsync(user, "Test!12345")).Succeeded);

            var unit = new OrganizationUnit
            {
                Code = "BILGI_ISLEM",
                Name = "Bilgi İşlem",
                UnitType = OrganizationUnitType.Branch,
                DisplayOrder = 1,
                IsActive = true
            };
            db.OrganizationUnits.Add(unit);
            await db.SaveChangesAsync();

            var profile = new InternProfile
            {
                UserId = user.Id,
                StudentNumber = "EXC-1",
                CurrentOrganizationUnitId = unit.Id,
                University = "Fırat",
                IsActive = true
            };
            db.InternProfiles.Add(profile);
            await db.SaveChangesAsync();

            var result = await export.ExportAsync(profile.Id);
            Assert.NotNull(result);
            Assert.Equal(InternExcelExportService.ContentType, result!.ContentType);
            Assert.Equal("Stajyer_Ayşe_Yılmaz_20260716.xlsx", result.FileName);
            Assert.True(result.Content.Length > 0);

            using var stream = new MemoryStream(result.Content);
            using var workbook = new XLWorkbook(stream);
            Assert.Contains(workbook.Worksheets, w => w.Name == "Temel Bilgiler");
            Assert.Contains(workbook.Worksheets, w => w.Name == "Projeler");
            Assert.Contains(workbook.Worksheets, w => w.Name == "Devam");
            Assert.Contains(workbook.Worksheets, w => w.Name == "Günlük Raporlar");
            Assert.Contains(workbook.Worksheets, w => w.Name == "İzinler");
            Assert.Contains(workbook.Worksheets, w => w.Name == "Değerlendirmeler");
            Assert.Equal("Kayıt bulunamadı", workbook.Worksheet("Projeler").Cell(1, 1).GetString());
        }
        finally
        {
            await sp.GetRequiredService<AppDbContext>().Database.EnsureDeletedAsync();
        }
    }

    [Fact]
    public void Sanitize_Prevents_Formula_Injection()
    {
        Assert.Equal("'=1+1", InternExcelExportService.Sanitize("=1+1"));
        Assert.Equal("'+CMD", InternExcelExportService.Sanitize("+CMD"));
        Assert.Equal("'-2", InternExcelExportService.Sanitize("-2"));
        Assert.Equal("'@SUM", InternExcelExportService.Sanitize("@SUM"));
        Assert.Equal("Normal", InternExcelExportService.Sanitize("Normal"));
    }

    private static async Task<ServiceProvider> CreateHostAsync(DateTime utc)
    {
        var dbName = "Staj360ExcelExport_" + Guid.NewGuid().ToString("N");
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IClock>(new TestClock(utc));
        services.AddSingleton<ITimeZoneService>(new FixedTimeZoneService());
        services.AddSingleton(Options.Create(new OrganizationOptions { Name = "Test", TimeZone = "Europe/Istanbul" }));
        services.AddDbContext<AppDbContext>(o =>
            o.UseSqlServer($"Server=localhost\\SQLEXPRESS01;Database={dbName};Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=true"));
        services.AddIdentity<ApplicationUser, ApplicationRole>()
            .AddEntityFrameworkStores<AppDbContext>()
            .AddDefaultTokenProviders();
        services.AddScoped<IInternExcelExportService, InternExcelExportService>();

        var sp = services.BuildServiceProvider();
        await sp.GetRequiredService<AppDbContext>().Database.EnsureCreatedAsync();
        return sp;
    }
}
