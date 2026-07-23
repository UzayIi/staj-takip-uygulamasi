using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Staj360.Application.Common;
using Staj360.Domain.Entities;
using Staj360.Domain.Enums;
using Staj360.Infrastructure.Identity;

namespace Staj360.Infrastructure.Persistence;

/// <summary>
/// Rolleri, sabit organizasyon birimlerini, isteğe bağlı ilk yöneticiyi ve geliştirme verilerini seed eder.
/// </summary>
public static class DbSeeder
{
    public static async Task SeedAsync(IServiceProvider services, bool isDevelopment, CancellationToken cancellationToken = default)
    {
        var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger("DbSeeder");
        var roleManager = services.GetRequiredService<RoleManager<ApplicationRole>>();
        var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
        var db = services.GetRequiredService<AppDbContext>();
        var config = services.GetRequiredService<IConfiguration>();

        await SeedRolesAsync(roleManager);
        await SeedAdminAsync(userManager, config, logger);
        await SeedOrganizationUnitsAsync(db, logger, cancellationToken);
        await SeedBaseDataAsync(db, cancellationToken);

        // Demo giriş hesapları: yalnızca Development + Seed:SampleData.
        await DemoDataSeeder.SeedAsync(services, isDevelopment, cancellationToken);
        await DemoDataSeeder.ResetDemoPasswordsAsync(services, isDevelopment, cancellationToken);
    }

    private static async Task SeedRolesAsync(RoleManager<ApplicationRole> roleManager)
    {
        foreach (var role in AppRoles.All)
        {
            if (!await roleManager.RoleExistsAsync(role))
                await roleManager.CreateAsync(new ApplicationRole(role));
        }
    }

    private static async Task SeedAdminAsync(UserManager<ApplicationUser> userManager, IConfiguration config, ILogger logger)
    {
        var email = Environment.GetEnvironmentVariable("SEED_ADMIN_EMAIL") ?? config["SEED_ADMIN_EMAIL"];
        var password = Environment.GetEnvironmentVariable("SEED_ADMIN_PASSWORD") ?? config["SEED_ADMIN_PASSWORD"];

        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            logger.LogInformation("SEED_ADMIN_EMAIL/PASSWORD tanımlı değil. Roller oluşturuldu, admin hesabı oluşturulmadı.");
            return;
        }

        if (await userManager.FindByEmailAsync(email) is not null)
            return;

        var admin = new ApplicationUser
        {
            UserName = email,
            Email = email,
            FullName = "Sistem Yöneticisi",
            IsActive = true,
            EmailConfirmed = true,
            CreatedAtUtc = DateTime.UtcNow
        };

        var result = await userManager.CreateAsync(admin, password);
        if (result.Succeeded)
        {
            await userManager.AddToRoleAsync(admin, AppRoles.SuperAdmin);
            logger.LogInformation("İlk SuperAdmin hesabı oluşturuldu: {Email}", email);
        }
        else
        {
            logger.LogWarning("SuperAdmin oluşturulamadı: {Errors}", string.Join("; ", result.Errors.Select(e => e.Description)));
        }
    }

    /// <summary>Resmî teşkilat şemasını Code üzerinden idempotent seed eder.</summary>
    public static async Task SeedOrganizationUnitsAsync(AppDbContext db, ILogger logger, CancellationToken cancellationToken)
    {
        var existing = await db.OrganizationUnits.IgnoreQueryFilters()
            .ToDictionaryAsync(u => u.Code, cancellationToken);

        var created = 0;
        var updated = 0;
        var codeToId = new Dictionary<string, Guid>(StringComparer.Ordinal);

        foreach (var def in OrganizationSeedCatalog.All.Where(d => d.Type == OrganizationUnitType.Directorate))
        {
            if (existing.TryGetValue(def.Code, out var unit))
            {
                if (unit.Name != def.Name || unit.DisplayOrder != def.DisplayOrder || unit.IsDeleted)
                {
                    unit.Name = def.Name;
                    unit.DisplayOrder = def.DisplayOrder;
                    unit.UnitType = OrganizationUnitType.Directorate;
                    unit.ParentId = null;
                    unit.IsDeleted = false;
                    unit.IsActive = true;
                    updated++;
                }
                codeToId[def.Code] = unit.Id;
            }
            else
            {
                var entity = new OrganizationUnit
                {
                    Code = def.Code,
                    Name = def.Name,
                    UnitType = OrganizationUnitType.Directorate,
                    ParentId = null,
                    DisplayOrder = def.DisplayOrder,
                    IsActive = true
                };
                db.OrganizationUnits.Add(entity);
                codeToId[def.Code] = entity.Id;
                created++;
            }
        }

        await db.SaveChangesAsync(cancellationToken);

        // Yeniden yükle (yeni Id'ler için).
        existing = await db.OrganizationUnits.IgnoreQueryFilters()
            .ToDictionaryAsync(u => u.Code, cancellationToken);
        foreach (var kv in existing)
            codeToId[kv.Key] = kv.Value.Id;

        foreach (var def in OrganizationSeedCatalog.All.Where(d => d.Type == OrganizationUnitType.Branch))
        {
            if (def.ParentCode is null || !codeToId.TryGetValue(def.ParentCode, out var parentId))
                continue;

            if (existing.TryGetValue(def.Code, out var unit))
            {
                if (unit.Name != def.Name || unit.ParentId != parentId || unit.DisplayOrder != def.DisplayOrder || unit.IsDeleted)
                {
                    unit.Name = def.Name;
                    unit.ParentId = parentId;
                    unit.DisplayOrder = def.DisplayOrder;
                    unit.UnitType = OrganizationUnitType.Branch;
                    unit.IsDeleted = false;
                    unit.IsActive = true;
                    updated++;
                }
            }
            else
            {
                db.OrganizationUnits.Add(new OrganizationUnit
                {
                    Code = def.Code,
                    Name = def.Name,
                    UnitType = OrganizationUnitType.Branch,
                    ParentId = parentId,
                    DisplayOrder = def.DisplayOrder,
                    IsActive = true
                });
                created++;
            }
        }

        await db.SaveChangesAsync(cancellationToken);
        logger.LogInformation(
            "Organizasyon seed tamamlandı (kaynak: {Url}). Oluşturulan: {Created}, güncellenen: {Updated}, katalog: {Total}",
            OrganizationSeedCatalog.SourceUrl, created, updated, OrganizationSeedCatalog.All.Count);
    }

    private static async Task SeedBaseDataAsync(AppDbContext db, CancellationToken cancellationToken)
    {
        if (!await db.WorkSchedules.AnyAsync(cancellationToken))
        {
            db.WorkSchedules.Add(new WorkSchedule
            {
                Name = "Standart Mesai (09:00-18:00, Hafta içi)",
                StartTime = new TimeOnly(9, 0),
                EndTime = new TimeOnly(18, 0),
                GracePeriodMinutes = 15,
                MondayEnabled = true,
                TuesdayEnabled = true,
                WednesdayEnabled = true,
                ThursdayEnabled = true,
                FridayEnabled = true
            });
        }

        await db.SaveChangesAsync(cancellationToken);
    }
}

