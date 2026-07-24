using Microsoft.EntityFrameworkCore;
using Staj360.Domain.Entities;

namespace Staj360.Application.Abstractions;

/// <summary>
/// Application katmanının veri erişimi için kullandığı sözleşme. Generic repository
/// yerine EF Core'un DbSet ve LINQ yetenekleri doğrudan kullanılır.
/// </summary>
public interface IApplicationDbContext
{
    DbSet<OrganizationUnit> OrganizationUnits { get; }
    DbSet<ManagerUnitAssignment> ManagerUnitAssignments { get; }
    DbSet<AdvisorUnitAssignment> AdvisorUnitAssignments { get; }
    DbSet<InternUnitAssignment> InternUnitAssignments { get; }
    DbSet<InternTransferRequest> InternTransferRequests { get; }
    DbSet<InternFeedback> InternFeedbacks { get; }
    DbSet<InternProfile> InternProfiles { get; }
    DbSet<InternshipPeriod> InternshipPeriods { get; }
    DbSet<WorkSchedule> WorkSchedules { get; }
    DbSet<AttendanceDay> AttendanceDays { get; }
    DbSet<AttendanceEvent> AttendanceEvents { get; }
    DbSet<AttendanceCorrectionRequest> AttendanceCorrectionRequests { get; }
    DbSet<DailyReport> DailyReports { get; }
    DbSet<DailyWorkItem> DailyWorkItems { get; }
    DbSet<Project> Projects { get; }
    DbSet<ProjectAssignment> ProjectAssignments { get; }
    DbSet<ProjectTask> ProjectTasks { get; }
    DbSet<LeaveRequest> LeaveRequests { get; }
    DbSet<Evaluation> Evaluations { get; }
    DbSet<Announcement> Announcements { get; }
    DbSet<Notification> Notifications { get; }
    DbSet<AuditLog> AuditLogs { get; }
    DbSet<StaffMessage> StaffMessages { get; }
    DbSet<AiReportSummary> AiReportSummaries { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
