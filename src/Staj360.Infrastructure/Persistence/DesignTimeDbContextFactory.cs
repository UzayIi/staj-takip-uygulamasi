using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Staj360.Infrastructure.Persistence;

/// <summary>
/// EF Core araçlarının (migration) tasarım zamanında DbContext oluşturabilmesi için.
/// Gerçek bağlantı çalışma zamanında appsettings/env üzerinden gelir.
/// </summary>
public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")
            ?? "Server=localhost\\SQLEXPRESS01;Database=Staj360Db;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True";

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer(connectionString, sql => sql.MigrationsAssembly(typeof(AppDbContext).Assembly.FullName))
            .Options;

        return new AppDbContext(options);
    }
}
