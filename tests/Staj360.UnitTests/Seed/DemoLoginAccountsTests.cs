using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Staj360.Application.Abstractions;
using Staj360.Application.Common;
using Staj360.Domain.Entities;
using Staj360.Domain.Enums;
using Staj360.Infrastructure.Identity;
using Staj360.Infrastructure.Persistence;
using Staj360.UnitTests.TestSupport;

namespace Staj360.UnitTests.Seed;

[Collection(DemoSeedEnvCollection.Name)]
public class DemoLoginAccountsTests
{
    private const string DemoPassword = "baris123";

    [Fact]
    public async Task Seed_Creates_Canonical_Emails_When_SampleData_Enabled()
    {
        await using var sp = await CreateHostAsync(sampleData: true, password: DemoPassword);
        try
        {
            await DemoDataSeeder.SeedAsync(sp, isDevelopment: true);
            var users = sp.GetRequiredService<UserManager<ApplicationUser>>();

            foreach (var email in DemoLoginAccounts.CanonicalEmails)
            {
                var user = await users.FindByEmailAsync(email);
                Assert.NotNull(user);
                Assert.True(user.EmailConfirmed);
                Assert.True(await users.CheckPasswordAsync(user, DemoPassword));
            }

            Assert.True(await users.IsInRoleAsync(
                (await users.FindByEmailAsync(DemoLoginAccounts.SuperAdminEmail))!, AppRoles.SuperAdmin));
            Assert.True(await users.IsInRoleAsync(
                (await users.FindByEmailAsync(DemoLoginAccounts.AdminEmail))!, AppRoles.Admin));
            Assert.True(await users.IsInRoleAsync(
                (await users.FindByEmailAsync(DemoLoginAccounts.ManagerEmail))!, AppRoles.Manager));
            Assert.True(await users.IsInRoleAsync(
                (await users.FindByEmailAsync(DemoLoginAccounts.MentorEmail))!, AppRoles.Mentor));
            Assert.True(await users.IsInRoleAsync(
                (await users.FindByEmailAsync(DemoLoginAccounts.InternEmail))!, AppRoles.Intern));
        }
        finally
        {
            await CleanupAsync(sp);
        }
    }

    [Fact]
    public async Task Seed_Is_Idempotent_And_Preserves_UserIds()
    {
        await using var sp = await CreateHostAsync(sampleData: true, password: DemoPassword);
        try
        {
            await DemoDataSeeder.SeedAsync(sp, isDevelopment: true);
            var users = sp.GetRequiredService<UserManager<ApplicationUser>>();
            var ids = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);
            foreach (var email in DemoLoginAccounts.CanonicalEmails)
                ids[email] = (await users.FindByEmailAsync(email))!.Id;

            await DemoDataSeeder.SeedAsync(sp, isDevelopment: true);

            Assert.Equal(5, await users.Users.CountAsync(u => DemoLoginAccounts.CanonicalEmails.Contains(u.Email!)));
            foreach (var email in DemoLoginAccounts.CanonicalEmails)
                Assert.Equal(ids[email], (await users.FindByEmailAsync(email))!.Id);
        }
        finally
        {
            await CleanupAsync(sp);
        }
    }

    [Fact]
    public async Task Seed_Migrates_Legacy_Email_Preserving_UserId()
    {
        await using var sp = await CreateHostAsync(sampleData: true, password: DemoPassword);
        try
        {
            var users = sp.GetRequiredService<UserManager<ApplicationUser>>();
            var legacy = new ApplicationUser
            {
                UserName = "admin.demo@stajamed.local",
                Email = "admin.demo@stajamed.local",
                FullName = "Legacy Super",
                IsActive = true,
                EmailConfirmed = true,
                CreatedAtUtc = DateTime.UtcNow
            };
            Assert.True((await users.CreateAsync(legacy, "Legacy!Pass1")).Succeeded);
            await users.AddToRoleAsync(legacy, AppRoles.SuperAdmin);
            var legacyId = legacy.Id;

            await DemoDataSeeder.SeedAsync(sp, isDevelopment: true);

            Assert.Null(await users.FindByEmailAsync("admin.demo@stajamed.local"));
            var migrated = await users.FindByEmailAsync(DemoLoginAccounts.SuperAdminEmail);
            Assert.NotNull(migrated);
            Assert.Equal(legacyId, migrated.Id);
            Assert.True(await users.CheckPasswordAsync(migrated, DemoPassword));
        }
        finally
        {
            await CleanupAsync(sp);
        }
    }

    [Fact]
    public async Task Seed_Does_Not_Run_When_SampleData_False()
    {
        await using var sp = await CreateHostAsync(sampleData: false, password: DemoPassword);
        try
        {
            await DemoDataSeeder.SeedAsync(sp, isDevelopment: true);
            var users = sp.GetRequiredService<UserManager<ApplicationUser>>();
            Assert.Null(await users.FindByEmailAsync(DemoLoginAccounts.SuperAdminEmail));
        }
        finally
        {
            await CleanupAsync(sp);
        }
    }

    [Fact]
    public async Task Seed_Does_Not_Run_Outside_Development()
    {
        await using var sp = await CreateHostAsync(sampleData: true, password: DemoPassword);
        try
        {
            await DemoDataSeeder.SeedAsync(sp, isDevelopment: false);
            var users = sp.GetRequiredService<UserManager<ApplicationUser>>();
            Assert.Null(await users.FindByEmailAsync(DemoLoginAccounts.InternEmail));
        }
        finally
        {
            await CleanupAsync(sp);
        }
    }

    [Fact]
    public async Task Seed_Does_Not_Create_Users_Without_Password()
    {
        await using var sp = await CreateHostAsync(sampleData: true, password: null);
        try
        {
            await DemoDataSeeder.SeedAsync(sp, isDevelopment: true);
            var users = sp.GetRequiredService<UserManager<ApplicationUser>>();
            Assert.Null(await users.FindByEmailAsync(DemoLoginAccounts.AdminEmail));
        }
        finally
        {
            await CleanupAsync(sp);
        }
    }

    [Fact]
    public async Task Development_Password_Policy_Accepts_Simple_Demo_Password()
    {
        await using var sp = await CreateHostAsync(sampleData: true, password: DemoPassword, relaxPassword: true);
        try
        {
            var users = sp.GetRequiredService<UserManager<ApplicationUser>>();
            var user = new ApplicationUser
            {
                UserName = "policy.check@test.local",
                Email = "policy.check@test.local",
                FullName = "Policy Check",
                IsActive = true,
                EmailConfirmed = true,
                CreatedAtUtc = DateTime.UtcNow
            };
            var result = await users.CreateAsync(user, DemoPassword);
            Assert.True(result.Succeeded, string.Join("; ", result.Errors.Select(e => e.Description)));
        }
        finally
        {
            await CleanupAsync(sp);
        }
    }

    [Fact]
    public async Task Reset_Does_Not_Run_Outside_Development()
    {
        await using var sp = await CreateHostAsync(sampleData: true, password: "OldDemo!123");
        try
        {
            await DemoDataSeeder.SeedAsync(sp, isDevelopment: true);
            var users = sp.GetRequiredService<UserManager<ApplicationUser>>();
            var demo = await users.FindByEmailAsync(DemoLoginAccounts.MarkerEmail);
            Assert.NotNull(demo);
            Assert.True(await users.CheckPasswordAsync(demo, "OldDemo!123"));

            await DemoDataSeeder.ResetDemoPasswordsAsync(sp, isDevelopment: false);

            demo = await users.FindByEmailAsync(DemoLoginAccounts.MarkerEmail);
            Assert.NotNull(demo);
            Assert.True(await users.CheckPasswordAsync(demo, "OldDemo!123"));
        }
        finally
        {
            await CleanupAsync(sp);
        }
    }

    [Fact]
    public async Task Reset_Does_Not_Run_When_SampleData_False()
    {
        await using var sp = await CreateHostAsync(sampleData: true, password: "OldDemo!123");
        try
        {
            await DemoDataSeeder.SeedAsync(sp, isDevelopment: true);
            var users = sp.GetRequiredService<UserManager<ApplicationUser>>();
            var demo = await users.FindByEmailAsync(DemoLoginAccounts.AdminEmail);
            Assert.NotNull(demo);
            Assert.True(await users.CheckPasswordAsync(demo, "OldDemo!123"));

            var cfg = sp.GetRequiredService<WritableConfig>();
            cfg.Set("Seed:SampleData", "false");
            cfg.Set("Seed:DemoPassword", "NewDemo!456");

            await DemoDataSeeder.ResetDemoPasswordsAsync(sp, isDevelopment: true);

            demo = await users.FindByEmailAsync(DemoLoginAccounts.AdminEmail);
            Assert.NotNull(demo);
            Assert.True(await users.CheckPasswordAsync(demo, "OldDemo!123"));
            Assert.False(await users.CheckPasswordAsync(demo, "NewDemo!456"));
        }
        finally
        {
            await CleanupAsync(sp);
        }
    }

    [Fact]
    public async Task Reset_Does_Not_Change_Non_Demo_User_Password()
    {
        await using var sp = await CreateHostAsync(sampleData: true, password: DemoPassword);
        try
        {
            await DemoDataSeeder.SeedAsync(sp, isDevelopment: true);
            var users = sp.GetRequiredService<UserManager<ApplicationUser>>();

            var real = new ApplicationUser
            {
                UserName = "real.user@company.local",
                Email = "real.user@company.local",
                FullName = "Gerçek Kullanıcı",
                IsActive = true,
                EmailConfirmed = true,
                CreatedAtUtc = DateTime.UtcNow
            };
            Assert.True((await users.CreateAsync(real, "RealUser!99")).Succeeded);

            await DemoDataSeeder.ResetDemoPasswordsAsync(sp, isDevelopment: true);

            real = await users.FindByEmailAsync("real.user@company.local");
            Assert.NotNull(real);
            Assert.True(await users.CheckPasswordAsync(real, "RealUser!99"));
            Assert.False(await users.CheckPasswordAsync(real, DemoPassword));
        }
        finally
        {
            await CleanupAsync(sp);
        }
    }

    [Fact]
    public async Task Email_Conflict_Does_Not_Delete_Users()
    {
        await using var sp = await CreateHostAsync(sampleData: true, password: DemoPassword);
        try
        {
            var users = sp.GetRequiredService<UserManager<ApplicationUser>>();

            var occupant = new ApplicationUser
            {
                UserName = DemoLoginAccounts.SuperAdminEmail,
                Email = DemoLoginAccounts.SuperAdminEmail,
                FullName = "Existing Occupant",
                IsActive = true,
                EmailConfirmed = true,
                CreatedAtUtc = DateTime.UtcNow
            };
            Assert.True((await users.CreateAsync(occupant, "Occupant!1")).Succeeded);
            var occupantId = occupant.Id;

            var legacy = new ApplicationUser
            {
                UserName = "admin.demo@stajamed.local",
                Email = "admin.demo@stajamed.local",
                FullName = "Legacy Super",
                IsActive = true,
                EmailConfirmed = true,
                CreatedAtUtc = DateTime.UtcNow
            };
            Assert.True((await users.CreateAsync(legacy, "Legacy!Pass1")).Succeeded);
            await users.AddToRoleAsync(legacy, AppRoles.SuperAdmin);
            var legacyId = legacy.Id;

            var ensure = await DemoLoginAccounts.EnsureAsync(
                users,
                sp.GetRequiredService<AppDbContext>(),
                DemoPassword,
                sp.GetRequiredService<ILoggerFactory>().CreateLogger("test"));

            // Hedef e-posta doluysa mevcut kullanıcı kullanılır; legacy silinmez / üzerine yazılmaz.
            Assert.True(ensure.Success);
            Assert.NotNull(await users.FindByIdAsync(occupantId.ToString()));
            Assert.NotNull(await users.FindByIdAsync(legacyId.ToString()));
            Assert.Equal(DemoLoginAccounts.SuperAdminEmail, (await users.FindByIdAsync(occupantId.ToString()))!.Email);
            Assert.Equal("admin.demo@stajamed.local", (await users.FindByIdAsync(legacyId.ToString()))!.Email);
            Assert.Equal(occupantId, ensure.UserIdsByEmail[DemoLoginAccounts.SuperAdminEmail]);
        }
        finally
        {
            await CleanupAsync(sp);
        }
    }

    private static async Task CleanupAsync(ServiceProvider sp)
    {
        await sp.GetRequiredService<AppDbContext>().Database.EnsureDeletedAsync();
        await sp.DisposeAsync();
    }

    private static async Task<ServiceProvider> CreateHostAsync(
        bool sampleData, string? password, bool relaxPassword = true)
    {
        var dbName = "Staj360DemoLogin_" + Guid.NewGuid().ToString("N");
        var writable = new WritableConfig();
        writable.Set("Seed:SampleData", sampleData ? "true" : "false");
        writable.Set("Seed:DemoPassword", password);

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(writable);
        services.AddSingleton(writable);
        services.AddLogging(b => b.AddProvider(NullLoggerProvider.Instance));
        services.AddSingleton<IClock>(new TestClock(new DateTime(2026, 7, 16, 8, 0, 0, DateTimeKind.Utc)));
        services.AddSingleton<ITimeZoneService>(new FixedTimeZoneService());
        services.AddDbContext<AppDbContext>(o =>
            o.UseSqlServer(
                $"Server=localhost\\SQLEXPRESS01;Database={dbName};Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=true"));

        services.AddIdentity<ApplicationUser, ApplicationRole>(o =>
            {
                if (relaxPassword)
                {
                    o.Password.RequiredLength = 1;
                    o.Password.RequireDigit = false;
                    o.Password.RequireLowercase = false;
                    o.Password.RequireUppercase = false;
                    o.Password.RequireNonAlphanumeric = false;
                    o.Password.RequiredUniqueChars = 0;
                }
                else
                {
                    o.Password.RequiredLength = 8;
                    o.Password.RequireDigit = true;
                    o.Password.RequireLowercase = true;
                    o.Password.RequireUppercase = true;
                    o.Password.RequireNonAlphanumeric = true;
                }

                o.User.RequireUniqueEmail = true;
            })
            .AddEntityFrameworkStores<AppDbContext>()
            .AddDefaultTokenProviders();

        var sp = services.BuildServiceProvider();
        var db = sp.GetRequiredService<AppDbContext>();
        await db.Database.EnsureCreatedAsync();
        await SeedOrganizationCatalogAsync(db);

        var roles = sp.GetRequiredService<RoleManager<ApplicationRole>>();
        foreach (var role in AppRoles.All)
        {
            if (!await roles.RoleExistsAsync(role))
                await roles.CreateAsync(new ApplicationRole(role));
        }

        return sp;
    }

    private static async Task SeedOrganizationCatalogAsync(AppDbContext db)
    {
        if (await db.OrganizationUnits.AnyAsync())
            return;

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
            Name = "Standart Mesai",
            StartTime = new TimeOnly(9, 0),
            EndTime = new TimeOnly(18, 0),
            GracePeriodMinutes = 15,
            MondayEnabled = true,
            TuesdayEnabled = true,
            WednesdayEnabled = true,
            ThursdayEnabled = true,
            FridayEnabled = true
        });

        await db.SaveChangesAsync();
    }

    /// <summary>Testlerde Seed anahtarlarını runtime'da değiştirebilmek için.</summary>
    private sealed class WritableConfig : IConfiguration
    {
        private readonly Dictionary<string, string?> _data = new(StringComparer.OrdinalIgnoreCase);

        public void Set(string key, string? value) => _data[key] = value;

        public string? this[string key]
        {
            get => _data.TryGetValue(key, out var v) ? v : null;
            set => _data[key] = value;
        }

        public IEnumerable<IConfigurationSection> GetChildren() => Array.Empty<IConfigurationSection>();

        public Microsoft.Extensions.Primitives.IChangeToken GetReloadToken() => new NoopChangeToken();

        public IConfigurationSection GetSection(string key) => new Section(this, key);

        private sealed class NoopChangeToken : Microsoft.Extensions.Primitives.IChangeToken
        {
            public bool HasChanged => false;
            public bool ActiveChangeCallbacks => false;
            public IDisposable RegisterChangeCallback(Action<object?> callback, object? state) => EmptyDisposable.Instance;
        }

        private sealed class EmptyDisposable : IDisposable
        {
            public static readonly EmptyDisposable Instance = new();
            public void Dispose() { }
        }

        private sealed class Section : IConfigurationSection
        {
            private readonly WritableConfig _root;
            private readonly string _path;

            public Section(WritableConfig root, string path)
            {
                _root = root;
                _path = path;
            }

            public string? this[string key]
            {
                get => _root[_path + ":" + key];
                set => _root[_path + ":" + key] = value;
            }

            public string Key => _path.Contains(':') ? _path[(_path.LastIndexOf(':') + 1)..] : _path;
            public string Path => _path;
            public string? Value
            {
                get => _root[_path];
                set => _root[_path] = value;
            }

            public IEnumerable<IConfigurationSection> GetChildren() => Array.Empty<IConfigurationSection>();
            public Microsoft.Extensions.Primitives.IChangeToken GetReloadToken() => new NoopChangeToken();
            public IConfigurationSection GetSection(string key) => new Section(_root, _path + ":" + key);
        }
    }

    private sealed class NullLoggerProvider : ILoggerProvider
    {
        public static readonly NullLoggerProvider Instance = new();
        public ILogger CreateLogger(string categoryName) => new NullLogger();
        public void Dispose() { }

        private sealed class NullLogger : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
            public bool IsEnabled(LogLevel logLevel) => false;
            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) { }
        }
    }
}
