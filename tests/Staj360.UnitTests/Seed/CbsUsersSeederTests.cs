using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Staj360.Application.Abstractions;
using Staj360.Application.Common;
using Staj360.Domain.Entities;
using Staj360.Domain.Enums;
using Staj360.Infrastructure.Identity;
using Staj360.Infrastructure.Persistence;
using Staj360.UnitTests.TestSupport;

namespace Staj360.UnitTests.Seed;

[Collection(DemoSeedEnvCollection.Name)]
public class CbsUsersSeederTests
{
    [Fact]
    public async Task Seed_Skipped_When_Password_Missing()
    {
        var previous = Environment.GetEnvironmentVariable("SEED_CBS_USERS_PASSWORD");
        Environment.SetEnvironmentVariable("SEED_CBS_USERS_PASSWORD", null);
        try
        {
            await using var sp = await CreateHostAsync(cbsPassword: null);
            try
            {
                await CbsUsersSeeder.SeedAsync(sp);
                var users = sp.GetRequiredService<UserManager<ApplicationUser>>();
                Assert.Null(await users.FindByEmailAsync(CbsUsersSeeder.FundaEmail));
                Assert.Null(await users.FindByEmailAsync("kaan.kesip@stajamed.local"));
            }
            finally
            {
                await CleanupAsync(sp);
            }
        }
        finally
        {
            Environment.SetEnvironmentVariable("SEED_CBS_USERS_PASSWORD", previous);
        }
    }

    [Fact]
    public async Task Seed_Is_Idempotent_When_Password_Present()
    {
        var previous = Environment.GetEnvironmentVariable("SEED_CBS_USERS_PASSWORD");
        Environment.SetEnvironmentVariable("SEED_CBS_USERS_PASSWORD", "CbsGucluParola1!");
        try
        {
            await using var sp = await CreateHostAsync(cbsPassword: "CbsGucluParola1!");
            try
            {
                await CbsUsersSeeder.SeedAsync(sp);
                var users = sp.GetRequiredService<UserManager<ApplicationUser>>();
                var db = sp.GetRequiredService<AppDbContext>();

                var funda = await users.FindByEmailAsync(CbsUsersSeeder.FundaEmail);
                Assert.NotNull(funda);
                var fundaId = funda!.Id;
                var internCount1 = await db.InternProfiles.CountAsync(p => p.StudentNumber.StartsWith("CBS-2026-"));

                await CbsUsersSeeder.SeedAsync(sp);

                Assert.Equal(fundaId, (await users.FindByEmailAsync(CbsUsersSeeder.FundaEmail))!.Id);
                Assert.Equal(internCount1, await db.InternProfiles.CountAsync(p => p.StudentNumber.StartsWith("CBS-2026-")));
                Assert.Equal(7, internCount1);
                Assert.True(await users.IsInRoleAsync(funda, AppRoles.Mentor));
            }
            finally
            {
                await CleanupAsync(sp);
            }
        }
        finally
        {
            Environment.SetEnvironmentVariable("SEED_CBS_USERS_PASSWORD", previous);
        }
    }

    private static async Task CleanupAsync(ServiceProvider sp)
    {
        await sp.GetRequiredService<AppDbContext>().Database.EnsureDeletedAsync();
        await sp.DisposeAsync();
    }

    private static async Task<ServiceProvider> CreateHostAsync(string? cbsPassword)
    {
        var dbName = "Staj360CbsSeed_" + Guid.NewGuid().ToString("N");
        var configData = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        if (cbsPassword is not null)
            configData["SEED_CBS_USERS_PASSWORD"] = cbsPassword;

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().AddInMemoryCollection(configData).Build());
        services.AddLogging(b => b.AddProvider(NullLoggerProvider.Instance));
        services.AddSingleton<IClock>(new TestClock(new DateTime(2026, 7, 16, 8, 0, 0, DateTimeKind.Utc)));
        services.AddSingleton<ITimeZoneService>(new FixedTimeZoneService());
        services.AddDbContext<AppDbContext>(o =>
            o.UseSqlServer(
                $"Server=localhost\\SQLEXPRESS01;Database={dbName};Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=true"));

        services.AddIdentity<ApplicationUser, ApplicationRole>(o =>
            {
                o.Password.RequiredLength = 1;
                o.Password.RequireDigit = false;
                o.Password.RequireLowercase = false;
                o.Password.RequireUppercase = false;
                o.Password.RequireNonAlphanumeric = false;
                o.User.RequireUniqueEmail = true;
            })
            .AddEntityFrameworkStores<AppDbContext>()
            .AddDefaultTokenProviders();

        var sp = services.BuildServiceProvider();
        var db = sp.GetRequiredService<AppDbContext>();
        await db.Database.EnsureCreatedAsync();

        var byCode = new Dictionary<string, OrganizationUnit>(StringComparer.Ordinal);
        foreach (var def in OrganizationSeedCatalog.All.Where(d => d.ParentCode is null))
        {
            var unit = new OrganizationUnit
            {
                Code = def.Code,
                Name = def.Name,
                UnitType = def.Type,
                DisplayOrder = def.DisplayOrder,
                IsActive = true
            };
            db.OrganizationUnits.Add(unit);
            byCode[def.Code] = unit;
        }
        await db.SaveChangesAsync();
        foreach (var def in OrganizationSeedCatalog.All.Where(d => d.ParentCode is not null))
        {
            db.OrganizationUnits.Add(new OrganizationUnit
            {
                Code = def.Code,
                Name = def.Name,
                UnitType = def.Type,
                ParentId = byCode[def.ParentCode!].Id,
                DisplayOrder = def.DisplayOrder,
                IsActive = true
            });
        }
        db.WorkSchedules.Add(new WorkSchedule
        {
            Name = "Standart",
            StartTime = new TimeOnly(9, 0),
            EndTime = new TimeOnly(18, 0),
            GracePeriodMinutes = 15,
            MondayEnabled = true, TuesdayEnabled = true, WednesdayEnabled = true,
            ThursdayEnabled = true, FridayEnabled = true
        });
        await db.SaveChangesAsync();

        var roles = sp.GetRequiredService<RoleManager<ApplicationRole>>();
        foreach (var role in AppRoles.All)
        {
            if (!await roles.RoleExistsAsync(role))
                await roles.CreateAsync(new ApplicationRole(role));
        }

        return sp;
    }
}
