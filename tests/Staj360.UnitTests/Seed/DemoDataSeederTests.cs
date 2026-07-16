using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Staj360.Application.Abstractions;
using Staj360.Application.Common;
using Staj360.Infrastructure.Configuration;
using Staj360.Infrastructure.Identity;
using Staj360.Infrastructure.Persistence;
using Staj360.Infrastructure.Services;
using Staj360.UnitTests.TestSupport;

namespace Staj360.UnitTests.Seed;

public class DemoDataSeederTests
{
    [Fact]
    public async Task DemoSeeder_Is_Idempotent_When_Run_Twice()
    {
        var utc = new DateTime(2026, 7, 16, 8, 0, 0, DateTimeKind.Utc);
        await using var sp = await CreateHostAsync(utc, seedDemo: true, password: "Demo!Pass123");
        try
        {
            await DemoDataSeeder.SeedAsync(sp, isDevelopment: true);
            await DemoDataSeeder.SeedAsync(sp, isDevelopment: true);

            var users = sp.GetRequiredService<UserManager<ApplicationUser>>();
            var db = sp.GetRequiredService<AppDbContext>();

            Assert.NotNull(await users.FindByEmailAsync("ayse.yilmaz@staj360.local"));
            Assert.Equal(1, await db.InternProfiles.CountAsync(p => p.StudentNumber == "DEMO-1001"));
            Assert.Equal(6, await db.InternProfiles.CountAsync(p => p.StudentNumber.StartsWith("DEMO-")));
            Assert.Equal(2, await db.Projects.CountAsync(p =>
                p.Name == "Kurumsal Talep Yönetim Sistemi" || p.Name == "Veri Analizi ve Raporlama Paneli"));
        }
        finally
        {
            sp.GetRequiredService<EnvRestore>().Restore();
            await sp.GetRequiredService<AppDbContext>().Database.EnsureDeletedAsync();
        }
    }

    [Fact]
    public async Task DemoSeeder_Does_Not_Create_Users_When_Flag_Disabled()
    {
        var utc = new DateTime(2026, 7, 16, 8, 0, 0, DateTimeKind.Utc);
        await using var sp = await CreateHostAsync(utc, seedDemo: false, password: "Demo!Pass123");
        try
        {
            await DemoDataSeeder.SeedAsync(sp, isDevelopment: true);
            var users = sp.GetRequiredService<UserManager<ApplicationUser>>();
            Assert.Null(await users.FindByEmailAsync(DemoDataSeeder.MarkerEmail));
            Assert.Null(await users.FindByEmailAsync("ayse.yilmaz@staj360.local"));
        }
        finally
        {
            sp.GetRequiredService<EnvRestore>().Restore();
            await sp.GetRequiredService<AppDbContext>().Database.EnsureDeletedAsync();
        }
    }

    [Fact]
    public async Task DemoSeeder_Does_Not_Run_In_Production()
    {
        var utc = new DateTime(2026, 7, 16, 8, 0, 0, DateTimeKind.Utc);
        await using var sp = await CreateHostAsync(utc, seedDemo: true, password: "Demo!Pass123");
        try
        {
            await DemoDataSeeder.SeedAsync(sp, isDevelopment: false);
            var users = sp.GetRequiredService<UserManager<ApplicationUser>>();
            Assert.Null(await users.FindByEmailAsync("ayse.yilmaz@staj360.local"));
        }
        finally
        {
            sp.GetRequiredService<EnvRestore>().Restore();
            await sp.GetRequiredService<AppDbContext>().Database.EnsureDeletedAsync();
        }
    }

    private static async Task<ServiceProvider> CreateHostAsync(DateTime utc, bool seedDemo, string? password)
    {
        var previousData = Environment.GetEnvironmentVariable("SEED_DEMO_DATA");
        var previousPass = Environment.GetEnvironmentVariable("SEED_DEMO_PASSWORD");
        Environment.SetEnvironmentVariable("SEED_DEMO_DATA", seedDemo ? "true" : "false");
        Environment.SetEnvironmentVariable("SEED_DEMO_PASSWORD", password);

        var dbName = "Staj360DemoSeed_" + Guid.NewGuid().ToString("N");
        var configValues = new Dictionary<string, string?>
        {
            ["SEED_DEMO_DATA"] = seedDemo ? "true" : "false",
            ["SEED_DEMO_PASSWORD"] = password
        };
        var config = new ConfigurationBuilder().AddInMemoryCollection(configValues).Build();

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(config);
        services.AddLogging(b => b.AddProvider(NullLoggerProvider.Instance));
        services.AddSingleton<IClock>(new TestClock(utc));
        services.AddSingleton<ITimeZoneService>(new FixedTimeZoneService());
        services.AddSingleton(Options.Create(new OrganizationOptions { Name = "Test", TimeZone = "Europe/Istanbul" }));
        services.AddDbContext<AppDbContext>(o =>
            o.UseSqlServer($"Server=localhost\\SQLEXPRESS01;Database={dbName};Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=true"));
        services.AddIdentity<ApplicationUser, ApplicationRole>(o =>
            {
                o.Password.RequiredLength = 8;
                o.Password.RequireDigit = true;
                o.Password.RequireLowercase = true;
                o.Password.RequireUppercase = true;
                o.Password.RequireNonAlphanumeric = true;
            })
            .AddEntityFrameworkStores<AppDbContext>()
            .AddDefaultTokenProviders();

        // Env restore on dispose via callback registered in returned provider cleanup by tests.
        services.AddSingleton(new EnvRestore(previousData, previousPass));

        var sp = services.BuildServiceProvider();
        var db = sp.GetRequiredService<AppDbContext>();
        await db.Database.EnsureCreatedAsync();

        var roles = sp.GetRequiredService<RoleManager<ApplicationRole>>();
        foreach (var role in AppRoles.All)
        {
            if (!await roles.RoleExistsAsync(role))
                await roles.CreateAsync(new ApplicationRole(role));
        }

        return sp;
    }
}

file sealed class EnvRestore
{
    private readonly string? _data;
    private readonly string? _pass;
    public EnvRestore(string? data, string? pass) { _data = data; _pass = pass; }
    public void Restore()
    {
        Environment.SetEnvironmentVariable("SEED_DEMO_DATA", _data);
        Environment.SetEnvironmentVariable("SEED_DEMO_PASSWORD", _pass);
    }
}

file sealed class NullLoggerProvider : ILoggerProvider
{
    public static readonly NullLoggerProvider Instance = new();
    public ILogger CreateLogger(string categoryName) => Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance;
    public void Dispose() { }
}
