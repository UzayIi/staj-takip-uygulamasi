using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Staj360.Application.Abstractions;
using Staj360.Domain.Common;
using Staj360.Domain.Entities;
using Staj360.Domain.Enums;
using Staj360.Infrastructure.Identity;

namespace Staj360.Infrastructure.Persistence;

/// <summary>
/// Uygulamanın EF Core veritabanı bağlamı. Identity ile birleşik çalışır ve
/// Application katmanının IApplicationDbContext sözleşmesini karşılar.
/// </summary>
public class AppDbContext : IdentityDbContext<ApplicationUser, ApplicationRole, Guid>, IApplicationDbContext
{
    private readonly IClock? _clock;
    private readonly ICurrentUserService? _currentUser;

    public AppDbContext(DbContextOptions<AppDbContext> options, IClock? clock = null, ICurrentUserService? currentUser = null)
        : base(options)
    {
        _clock = clock;
        _currentUser = currentUser;
    }

    public DbSet<OrganizationUnit> OrganizationUnits => Set<OrganizationUnit>();
    public DbSet<ManagerUnitAssignment> ManagerUnitAssignments => Set<ManagerUnitAssignment>();
    public DbSet<AdvisorUnitAssignment> AdvisorUnitAssignments => Set<AdvisorUnitAssignment>();
    public DbSet<InternUnitAssignment> InternUnitAssignments => Set<InternUnitAssignment>();
    public DbSet<InternTransferRequest> InternTransferRequests => Set<InternTransferRequest>();
    public DbSet<InternFeedback> InternFeedbacks => Set<InternFeedback>();
    public DbSet<InternProfile> InternProfiles => Set<InternProfile>();
    public DbSet<InternshipPeriod> InternshipPeriods => Set<InternshipPeriod>();
    public DbSet<WorkSchedule> WorkSchedules => Set<WorkSchedule>();
    public DbSet<AttendanceDay> AttendanceDays => Set<AttendanceDay>();
    public DbSet<AttendanceEvent> AttendanceEvents => Set<AttendanceEvent>();
    public DbSet<AttendanceCorrectionRequest> AttendanceCorrectionRequests => Set<AttendanceCorrectionRequest>();
    public DbSet<DailyReport> DailyReports => Set<DailyReport>();
    public DbSet<DailyWorkItem> DailyWorkItems => Set<DailyWorkItem>();
    public DbSet<Project> Projects => Set<Project>();
    public DbSet<ProjectAssignment> ProjectAssignments => Set<ProjectAssignment>();
    public DbSet<ProjectTask> ProjectTasks => Set<ProjectTask>();
    public DbSet<LeaveRequest> LeaveRequests => Set<LeaveRequest>();
    public DbSet<Evaluation> Evaluations => Set<Evaluation>();
    public DbSet<Announcement> Announcements => Set<Announcement>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<AiReportSummary> AiReportSummaries => Set<AiReportSummary>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

        foreach (var entityType in builder.Model.GetEntityTypes())
        {
            if (typeof(AuditableEntity).IsAssignableFrom(entityType.ClrType))
            {
                builder.Entity(entityType.ClrType)
                    .Property(nameof(AuditableEntity.RowVersion))
                    .IsRowVersion();
            }
        }
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        base.ConfigureConventions(configurationBuilder);

        configurationBuilder.Properties<InternshipStatus>().HaveConversion<string>().HaveMaxLength(30);
        configurationBuilder.Properties<AttendanceStatus>().HaveConversion<string>().HaveMaxLength(30);
        configurationBuilder.Properties<AttendanceEventType>().HaveConversion<string>().HaveMaxLength(30);
        configurationBuilder.Properties<AttendanceSource>().HaveConversion<string>().HaveMaxLength(30);
        configurationBuilder.Properties<CorrectionRequestStatus>().HaveConversion<string>().HaveMaxLength(30);
        configurationBuilder.Properties<DailyReportStatus>().HaveConversion<string>().HaveMaxLength(30);
        configurationBuilder.Properties<ProjectStatus>().HaveConversion<string>().HaveMaxLength(30);
        configurationBuilder.Properties<ProjectTaskStatus>().HaveConversion<string>().HaveMaxLength(30);
        configurationBuilder.Properties<TaskPriority>().HaveConversion<string>().HaveMaxLength(30);
        configurationBuilder.Properties<LeaveRequestStatus>().HaveConversion<string>().HaveMaxLength(30);
        configurationBuilder.Properties<LeaveType>().HaveConversion<string>().HaveMaxLength(30);
        configurationBuilder.Properties<AiSummaryStatus>().HaveConversion<string>().HaveMaxLength(30);
        configurationBuilder.Properties<AiSummaryType>().HaveConversion<string>().HaveMaxLength(30);
        configurationBuilder.Properties<NotificationType>().HaveConversion<string>().HaveMaxLength(30);
        configurationBuilder.Properties<OrganizationUnitType>().HaveConversion<string>().HaveMaxLength(30);
        configurationBuilder.Properties<TransferRequestStatus>().HaveConversion<string>().HaveMaxLength(30);
        configurationBuilder.Properties<FeedbackStatus>().HaveConversion<string>().HaveMaxLength(30);
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        ApplyAuditInformation();
        return base.SaveChangesAsync(cancellationToken);
    }

    public override int SaveChanges()
    {
        ApplyAuditInformation();
        return base.SaveChanges();
    }

    private void ApplyAuditInformation()
    {
        var now = _clock?.UtcNow ?? DateTime.UtcNow;
        var userId = _currentUser?.UserId;

        foreach (var entry in ChangeTracker.Entries<AuditableEntity>())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    entry.Entity.CreatedAtUtc = now;
                    entry.Entity.CreatedByUserId ??= userId;
                    break;
                case EntityState.Modified:
                    entry.Entity.UpdatedAtUtc = now;
                    entry.Entity.UpdatedByUserId = userId;
                    break;
            }
        }
    }
}
