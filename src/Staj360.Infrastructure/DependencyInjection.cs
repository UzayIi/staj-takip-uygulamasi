using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Staj360.Application.Abstractions;
using Staj360.Application.Ai;
using Staj360.Application.Services.Accounts;
using Staj360.Application.Services.Internships;
using Staj360.Infrastructure.Ai;
using Staj360.Infrastructure.Configuration;
using Staj360.Infrastructure.Identity;
using Staj360.Infrastructure.Persistence;
using Staj360.Infrastructure.Services;

namespace Staj360.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration, string uploadsRootPath)
    {
        // Veritabanı
        var connectionString = configuration.GetConnectionString("DefaultConnection");
        services.AddDbContext<AppDbContext>(options =>
            options.UseSqlServer(connectionString, sql => sql.MigrationsAssembly(typeof(AppDbContext).Assembly.FullName)));

        services.AddScoped<IApplicationDbContext>(sp => sp.GetRequiredService<AppDbContext>());

        // Identity: güçlü parola politikası ve hesap kilitleme.
        services.AddIdentity<ApplicationUser, ApplicationRole>(options =>
        {
            options.Password.RequiredLength = 8;
            options.Password.RequireUppercase = true;
            options.Password.RequireLowercase = true;
            options.Password.RequireDigit = true;
            options.Password.RequireNonAlphanumeric = true;

            options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
            options.Lockout.MaxFailedAccessAttempts = 5;
            options.Lockout.AllowedForNewUsers = true;

            options.User.RequireUniqueEmail = true;
            options.SignIn.RequireConfirmedAccount = false;
        })
        .AddEntityFrameworkStores<AppDbContext>()
        .AddDefaultTokenProviders();

        // Options pattern + başlangıç doğrulaması
        services.AddOptions<OrganizationOptions>().Bind(configuration.GetSection(OrganizationOptions.SectionName)).ValidateOnStart();
        services.AddOptions<AttendanceOptions>().Bind(configuration.GetSection(AttendanceOptions.SectionName)).ValidateOnStart();
        services.AddOptions<OpenAiOptions>()
            .Bind(configuration.GetSection(OpenAiOptions.SectionName))
            .PostConfigure(opts =>
            {
                // API anahtarı YALNIZCA OPENAI_API_KEY env veya User Secrets üzerinden.
                var envKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
                if (!string.IsNullOrWhiteSpace(envKey))
                    opts.ApiKey = envKey;
            })
            .ValidateOnStart();

        // Ortak servisler
        services.AddSingleton<IClock, SystemClock>();
        services.AddSingleton<ITimeZoneService, TimeZoneService>();
        services.AddScoped<IAuditLogService, AuditLogService>();
        services.AddScoped<IUserAccountService, UserAccountService>();
        services.AddScoped<IUserDisplayLookup, UserDisplayLookup>();
        services.AddScoped<IInternService, InternService>();

        services.AddScoped<IFileStorageService>(sp =>
            new LocalFileStorageService(uploadsRootPath, sp.GetRequiredService<ILogger<LocalFileStorageService>>()));

        // Yapay zekâ
        services.AddScoped<IAiProvider, OpenAiProvider>();

        var aiEnabled = configuration.GetValue<bool>($"{OpenAiOptions.SectionName}:Enabled")
            && (!string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("OPENAI_API_KEY"))
                || !string.IsNullOrWhiteSpace(configuration[$"{OpenAiOptions.SectionName}:ApiKey"]));

        if (aiEnabled)
            services.AddScoped<IReportSummaryService, OpenAiReportSummaryService>();
        else
            services.AddScoped<IReportSummaryService, NullReportSummaryService>();

        return services;
    }
}
