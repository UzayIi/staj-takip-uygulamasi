using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Staj360.Infrastructure.Persistence;

#nullable disable

namespace Staj360.Infrastructure.Persistence.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20260724120000_NormalizeDemoStudentNumbers")]
public class NormalizeDemoStudentNumbers : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            UPDATE ip
            SET StudentNumber = CONCAT(N'STJ-2026-', SUBSTRING(StudentNumber, 6, 50))
            FROM InternProfiles ip
            WHERE StudentNumber LIKE N'DEMO-%'
              AND StudentNumber NOT LIKE N'DEMO-%[^0-9]%'
              AND LEN(StudentNumber) > 5
              AND NOT EXISTS (
                  SELECT 1
                  FROM InternProfiles other
                  WHERE other.Id <> ip.Id
                    AND other.StudentNumber = CONCAT(N'STJ-2026-', SUBSTRING(ip.StudentNumber, 6, 50))
              );
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            UPDATE ip
            SET StudentNumber = CONCAT(N'DEMO-', SUBSTRING(StudentNumber, 10, 50))
            FROM InternProfiles ip
            WHERE StudentNumber LIKE N'STJ-2026-%'
              AND StudentNumber NOT LIKE N'STJ-2026-%[^0-9]%'
              AND LEN(StudentNumber) > 9
              AND NOT EXISTS (
                  SELECT 1
                  FROM InternProfiles other
                  WHERE other.Id <> ip.Id
                    AND other.StudentNumber = CONCAT(N'DEMO-', SUBSTRING(ip.StudentNumber, 10, 50))
              );
            """);
    }
}
