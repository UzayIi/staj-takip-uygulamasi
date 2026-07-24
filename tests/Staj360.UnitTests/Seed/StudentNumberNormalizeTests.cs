using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Staj360.Domain.Entities;
using Staj360.Domain.Enums;
using Staj360.Infrastructure.Persistence;
using Staj360.UnitTests.TestSupport;

namespace Staj360.UnitTests.Seed;

public class StudentNumberNormalizeTests
{
    [Fact]
    public async Task Normalize_Converts_Demo_To_Stj_When_Target_Free()
    {
        var clock = new TestClock(DateTime.UtcNow);
        await using var db = TestDbFactory.Create(clock);
        try
        {
            var unit = new OrganizationUnit
            {
                Code = "T",
                Name = "Test",
                UnitType = OrganizationUnitType.Branch,
                IsActive = true,
                DisplayOrder = 1
            };
            db.OrganizationUnits.Add(unit);
            await db.SaveChangesAsync();

            db.InternProfiles.Add(new InternProfile
            {
                UserId = Guid.NewGuid(),
                StudentNumber = "DEMO-2001",
                CurrentOrganizationUnitId = unit.Id,
                IsActive = true
            });
            db.InternProfiles.Add(new InternProfile
            {
                UserId = Guid.NewGuid(),
                StudentNumber = "DEMO-1001",
                CurrentOrganizationUnitId = unit.Id,
                IsActive = true
            });
            await db.SaveChangesAsync();

            await DemoLoginAccounts.NormalizeLegacyStudentNumbersAsync(db, NullLogger.Instance);

            var numbers = await db.InternProfiles.Select(p => p.StudentNumber).OrderBy(x => x).ToListAsync();
            Assert.Contains("STJ-2026-2001", numbers);
            Assert.Contains("STJ-2026-1001", numbers);
            Assert.DoesNotContain(numbers, n => n.StartsWith("DEMO-", StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            await TestDbFactory.DisposeDatabaseAsync(db);
        }
    }

    [Fact]
    public async Task Normalize_Skips_When_Target_Already_Taken()
    {
        var clock = new TestClock(DateTime.UtcNow);
        await using var db = TestDbFactory.Create(clock);
        try
        {
            var unit = new OrganizationUnit
            {
                Code = "T2",
                Name = "Test2",
                UnitType = OrganizationUnitType.Branch,
                IsActive = true,
                DisplayOrder = 1
            };
            db.OrganizationUnits.Add(unit);
            await db.SaveChangesAsync();

            db.InternProfiles.Add(new InternProfile
            {
                UserId = Guid.NewGuid(),
                StudentNumber = "STJ-2026-2001",
                CurrentOrganizationUnitId = unit.Id,
                IsActive = true
            });
            db.InternProfiles.Add(new InternProfile
            {
                UserId = Guid.NewGuid(),
                StudentNumber = "DEMO-2001",
                CurrentOrganizationUnitId = unit.Id,
                IsActive = true
            });
            await db.SaveChangesAsync();

            await DemoLoginAccounts.NormalizeLegacyStudentNumbersAsync(db, NullLogger.Instance);

            Assert.Equal(1, await db.InternProfiles.CountAsync(p => p.StudentNumber == "STJ-2026-2001"));
            Assert.Equal(1, await db.InternProfiles.CountAsync(p => p.StudentNumber == "DEMO-2001"));
        }
        finally
        {
            await TestDbFactory.DisposeDatabaseAsync(db);
        }
    }

    [Fact]
    public async Task Normalize_Is_Idempotent()
    {
        var clock = new TestClock(DateTime.UtcNow);
        await using var db = TestDbFactory.Create(clock);
        try
        {
            var unit = new OrganizationUnit
            {
                Code = "T3",
                Name = "Test3",
                UnitType = OrganizationUnitType.Branch,
                IsActive = true,
                DisplayOrder = 1
            };
            db.OrganizationUnits.Add(unit);
            await db.SaveChangesAsync();
            db.InternProfiles.Add(new InternProfile
            {
                UserId = Guid.NewGuid(),
                StudentNumber = "DEMO-2007",
                CurrentOrganizationUnitId = unit.Id,
                IsActive = true
            });
            await db.SaveChangesAsync();

            await DemoLoginAccounts.NormalizeLegacyStudentNumbersAsync(db, NullLogger.Instance);
            await DemoLoginAccounts.NormalizeLegacyStudentNumbersAsync(db, NullLogger.Instance);

            Assert.Equal(1, await db.InternProfiles.CountAsync(p => p.StudentNumber == "STJ-2026-2007"));
        }
        finally
        {
            await TestDbFactory.DisposeDatabaseAsync(db);
        }
    }
}
