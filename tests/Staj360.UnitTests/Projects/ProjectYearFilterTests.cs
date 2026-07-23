using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Staj360.Application.Abstractions;
using Staj360.Application.Common;
using Staj360.Application.Services.Projects;
using Staj360.Domain.Entities;
using Staj360.Domain.Enums;
using Staj360.Infrastructure.Persistence;
using Staj360.UnitTests.TestSupport;

namespace Staj360.UnitTests.Projects;

public class ProjectYearFilterTests
{
    [Fact]
    public async Task ListAsync_YearFilter_Returns_Only_Matching_Years()
    {
        var utc = new DateTime(2026, 7, 16, 10, 0, 0, DateTimeKind.Utc);
        await using var sp = await CreateHostAsync(utc);
        try
        {
            var db = sp.GetRequiredService<AppDbContext>();
            var service = sp.GetRequiredService<IProjectService>();

            var unit = new OrganizationUnit
            {
                Code = "YAZILIM",
                Name = "Yazılım",
                UnitType = OrganizationUnitType.Branch,
                DisplayOrder = 1,
                IsActive = true
            };
            db.OrganizationUnits.Add(unit);
            await db.SaveChangesAsync();

            var mentorId = Guid.NewGuid();
            db.Projects.Add(new Project
            {
                Name = "Proje 2025",
                StartDate = new DateOnly(2025, 3, 1),
                OrganizationUnitId = unit.Id,
                MentorUserId = mentorId,
                Status = ProjectStatus.Completed,
                ProgressPercentage = 100,
                CreatedAtUtc = new DateTime(2025, 2, 1, 0, 0, 0, DateTimeKind.Utc)
            });
            db.Projects.Add(new Project
            {
                Name = "Proje 2026",
                StartDate = new DateOnly(2026, 1, 15),
                OrganizationUnitId = unit.Id,
                MentorUserId = mentorId,
                Status = ProjectStatus.InProgress,
                ProgressPercentage = 40,
                CreatedAtUtc = utc
            });
            await db.SaveChangesAsync();

            var years = await service.GetAvailableYearsAsync();
            Assert.Contains(2026, years);
            Assert.Contains(2025, years);
            Assert.Equal(years.OrderByDescending(y => y), years);

            var filtered = await service.ListAsync(2026, 1, 20);
            Assert.Single(filtered.Items);
            Assert.Equal("Proje 2026", filtered.Items[0].Name);

            var all = await service.ListAsync(null, 1, 20);
            Assert.Equal(2, all.Items.Count);
        }
        finally
        {
            await sp.GetRequiredService<AppDbContext>().Database.EnsureDeletedAsync();
        }
    }

    private static async Task<ServiceProvider> CreateHostAsync(DateTime utc)
    {
        var dbName = "Staj360ProjectYear_" + Guid.NewGuid().ToString("N");
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IClock>(new TestClock(utc));
        services.AddSingleton<IAuditLogService, NoOpAuditLogService>();
        services.AddDbContext<AppDbContext>(o =>
            o.UseSqlServer($"Server=localhost\\SQLEXPRESS01;Database={dbName};Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=true"));
        services.AddScoped<IApplicationDbContext>(sp => sp.GetRequiredService<AppDbContext>());
        services.AddScoped<IProjectService, ProjectService>();

        var sp = services.BuildServiceProvider();
        await sp.GetRequiredService<AppDbContext>().Database.EnsureCreatedAsync();
        return sp;
    }
}
