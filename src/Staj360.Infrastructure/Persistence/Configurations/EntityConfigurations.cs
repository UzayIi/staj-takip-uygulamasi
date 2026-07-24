using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Staj360.Domain.Entities;

namespace Staj360.Infrastructure.Persistence.Configurations;

public class OrganizationUnitConfiguration : IEntityTypeConfiguration<OrganizationUnit>
{
    public void Configure(EntityTypeBuilder<OrganizationUnit> b)
    {
        b.Property(x => x.Code).HasMaxLength(80).IsRequired();
        b.Property(x => x.Name).HasMaxLength(250).IsRequired();
        b.HasIndex(x => x.Code).IsUnique();
        b.HasIndex(x => x.ParentId);
        b.HasIndex(x => x.DisplayOrder);

        b.HasOne(x => x.Parent)
            .WithMany(x => x.Children)
            .HasForeignKey(x => x.ParentId)
            .OnDelete(DeleteBehavior.Restrict);

        b.HasQueryFilter(x => !x.IsDeleted);
    }
}

public class ManagerUnitAssignmentConfiguration : IEntityTypeConfiguration<ManagerUnitAssignment>
{
    public void Configure(EntityTypeBuilder<ManagerUnitAssignment> b)
    {
        b.HasIndex(x => new { x.ManagerUserId, x.OrganizationUnitId })
            .IsUnique()
            .HasFilter("[IsActive] = 1 AND [IsDeleted] = 0");
        b.HasIndex(x => x.OrganizationUnitId);

        b.HasOne(x => x.OrganizationUnit)
            .WithMany(u => u.ManagerAssignments)
            .HasForeignKey(x => x.OrganizationUnitId)
            .OnDelete(DeleteBehavior.Restrict);

        b.HasQueryFilter(x => !x.IsDeleted);
    }
}

public class AdvisorUnitAssignmentConfiguration : IEntityTypeConfiguration<AdvisorUnitAssignment>
{
    public void Configure(EntityTypeBuilder<AdvisorUnitAssignment> b)
    {
        b.HasIndex(x => new { x.AdvisorUserId, x.OrganizationUnitId })
            .IsUnique()
            .HasFilter("[IsActive] = 1 AND [IsDeleted] = 0");
        b.HasIndex(x => x.OrganizationUnitId);

        b.HasOne(x => x.OrganizationUnit)
            .WithMany(u => u.AdvisorAssignments)
            .HasForeignKey(x => x.OrganizationUnitId)
            .OnDelete(DeleteBehavior.Restrict);

        b.HasQueryFilter(x => !x.IsDeleted);
    }
}

public class InternUnitAssignmentConfiguration : IEntityTypeConfiguration<InternUnitAssignment>
{
    public void Configure(EntityTypeBuilder<InternUnitAssignment> b)
    {
        b.HasIndex(x => new { x.InternProfileId, x.IsActive });
        b.HasIndex(x => x.OrganizationUnitId);
        b.HasIndex(x => x.AdvisorUserId);

        b.HasOne(x => x.InternProfile)
            .WithMany(i => i.UnitAssignments)
            .HasForeignKey(x => x.InternProfileId)
            .OnDelete(DeleteBehavior.Restrict);

        b.HasOne(x => x.OrganizationUnit)
            .WithMany(u => u.InternAssignments)
            .HasForeignKey(x => x.OrganizationUnitId)
            .OnDelete(DeleteBehavior.Restrict);

        b.HasQueryFilter(x => !x.IsDeleted);
    }
}

public class InternTransferRequestConfiguration : IEntityTypeConfiguration<InternTransferRequest>
{
    public void Configure(EntityTypeBuilder<InternTransferRequest> b)
    {
        b.Property(x => x.RequestNote).HasMaxLength(2000);
        b.Property(x => x.DecisionNote).HasMaxLength(2000);
        b.HasIndex(x => x.Status);
        b.HasIndex(x => x.TargetOrganizationUnitId);
        b.HasIndex(x => x.InternProfileId);
        b.HasIndex(x => x.PlannedStartDate);

        b.HasOne(x => x.InternProfile)
            .WithMany(i => i.TransferRequests)
            .HasForeignKey(x => x.InternProfileId)
            .OnDelete(DeleteBehavior.Restrict);

        b.HasOne(x => x.SourceOrganizationUnit)
            .WithMany()
            .HasForeignKey(x => x.SourceOrganizationUnitId)
            .OnDelete(DeleteBehavior.Restrict);

        b.HasOne(x => x.TargetOrganizationUnit)
            .WithMany()
            .HasForeignKey(x => x.TargetOrganizationUnitId)
            .OnDelete(DeleteBehavior.Restrict);

        b.HasQueryFilter(x => !x.IsDeleted);
    }
}

public class InternFeedbackConfiguration : IEntityTypeConfiguration<InternFeedback>
{
    public void Configure(EntityTypeBuilder<InternFeedback> b)
    {
        b.Property(x => x.Title).HasMaxLength(250).IsRequired();
        b.Property(x => x.Message).HasMaxLength(4000).IsRequired();
        b.Property(x => x.ReplyMessage).HasMaxLength(4000);
        b.Property(x => x.EscalationNote).HasMaxLength(2000);
        b.HasIndex(x => x.AdvisorUserId);
        b.HasIndex(x => x.InternProfileId);
        b.HasIndex(x => x.Status);

        b.HasOne(x => x.InternProfile)
            .WithMany(i => i.Feedbacks)
            .HasForeignKey(x => x.InternProfileId)
            .OnDelete(DeleteBehavior.Restrict);

        b.HasQueryFilter(x => !x.IsDeleted);
    }
}

public class InternProfileConfiguration : IEntityTypeConfiguration<InternProfile>
{
    public void Configure(EntityTypeBuilder<InternProfile> b)
    {
        b.Property(x => x.StudentNumber).HasMaxLength(50).IsRequired();
        b.Property(x => x.NationalId).HasMaxLength(20);
        b.Property(x => x.University).HasMaxLength(200);
        b.Property(x => x.Faculty).HasMaxLength(200);
        b.Property(x => x.SchoolDepartment).HasMaxLength(200);
        b.Property(x => x.ClassLevel).HasMaxLength(50);
        b.Property(x => x.PhoneNumber).HasMaxLength(30);
        b.Property(x => x.Address).HasMaxLength(500);
        b.Property(x => x.EmergencyContactName).HasMaxLength(150);
        b.Property(x => x.EmergencyContactPhone).HasMaxLength(30);

        b.HasIndex(x => x.UserId).IsUnique();
        b.HasIndex(x => x.StudentNumber);
        b.HasIndex(x => x.CurrentOrganizationUnitId);

        b.HasOne(x => x.CurrentOrganizationUnit)
            .WithMany(d => d.Interns)
            .HasForeignKey(x => x.CurrentOrganizationUnitId)
            .OnDelete(DeleteBehavior.Restrict);

        b.HasQueryFilter(x => !x.IsDeleted);
    }
}

public class WorkScheduleConfiguration : IEntityTypeConfiguration<WorkSchedule>
{
    public void Configure(EntityTypeBuilder<WorkSchedule> b)
    {
        b.Property(x => x.Name).HasMaxLength(150).IsRequired();
        b.HasQueryFilter(x => !x.IsDeleted);
    }
}

public class InternshipPeriodConfiguration : IEntityTypeConfiguration<InternshipPeriod>
{
    public void Configure(EntityTypeBuilder<InternshipPeriod> b)
    {
        b.HasIndex(x => x.Status);
        b.HasIndex(x => x.MentorUserId);
        b.HasIndex(x => new { x.StartDate, x.EndDate });

        b.HasOne(x => x.InternProfile)
            .WithMany(i => i.InternshipPeriods)
            .HasForeignKey(x => x.InternProfileId)
            .OnDelete(DeleteBehavior.Restrict);

        b.HasOne(x => x.WorkSchedule)
            .WithMany()
            .HasForeignKey(x => x.WorkScheduleId)
            .OnDelete(DeleteBehavior.Restrict);

        b.HasQueryFilter(x => !x.IsDeleted);
    }
}

public class AttendanceDayConfiguration : IEntityTypeConfiguration<AttendanceDay>
{
    public void Configure(EntityTypeBuilder<AttendanceDay> b)
    {
        // Aynı staj dönemi ve tarih için yalnızca bir AttendanceDay olabilir.
        b.HasIndex(x => new { x.InternshipPeriodId, x.WorkDate }).IsUnique();
        b.HasIndex(x => x.WorkDate);
        b.HasIndex(x => x.Status);

        b.HasOne(x => x.InternshipPeriod)
            .WithMany(p => p.AttendanceDays)
            .HasForeignKey(x => x.InternshipPeriodId)
            .OnDelete(DeleteBehavior.Restrict);

        b.HasQueryFilter(x => !x.IsDeleted);
    }
}

public class AttendanceEventConfiguration : IEntityTypeConfiguration<AttendanceEvent>
{
    public void Configure(EntityTypeBuilder<AttendanceEvent> b)
    {
        b.Property(x => x.IpAddress).HasMaxLength(64);
        b.Property(x => x.DeviceInfo).HasMaxLength(512);
        b.Property(x => x.Notes).HasMaxLength(1000);
        b.HasIndex(x => x.EventTimeUtc);

        b.HasOne(x => x.AttendanceDay)
            .WithMany(d => d.Events)
            .HasForeignKey(x => x.AttendanceDayId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasQueryFilter(x => !x.IsDeleted);
    }
}

public class AttendanceCorrectionRequestConfiguration : IEntityTypeConfiguration<AttendanceCorrectionRequest>
{
    public void Configure(EntityTypeBuilder<AttendanceCorrectionRequest> b)
    {
        b.Property(x => x.Reason).HasMaxLength(1000).IsRequired();
        b.Property(x => x.ReviewerNote).HasMaxLength(1000);
        b.HasIndex(x => x.Status);

        b.HasOne(x => x.AttendanceDay)
            .WithMany()
            .HasForeignKey(x => x.AttendanceDayId)
            .OnDelete(DeleteBehavior.Restrict);

        b.HasQueryFilter(x => !x.IsDeleted);
    }
}

public class DailyReportConfiguration : IEntityTypeConfiguration<DailyReport>
{
    public void Configure(EntityTypeBuilder<DailyReport> b)
    {
        b.Property(x => x.GeneralNotes).HasMaxLength(4000);
        b.Property(x => x.ProblemsEncountered).HasMaxLength(4000);
        b.Property(x => x.SolutionsApplied).HasMaxLength(4000);
        b.Property(x => x.TomorrowPlan).HasMaxLength(4000);
        b.Property(x => x.MentorComment).HasMaxLength(2000);

        // Aynı staj dönemi ve tarih için yalnızca bir DailyReport olabilir.
        b.HasIndex(x => new { x.InternshipPeriodId, x.ReportDate }).IsUnique();
        b.HasIndex(x => x.Status);
        b.HasIndex(x => x.OrganizationUnitId);

        b.HasOne(x => x.InternshipPeriod)
            .WithMany(p => p.DailyReports)
            .HasForeignKey(x => x.InternshipPeriodId)
            .OnDelete(DeleteBehavior.Restrict);

        b.HasOne(x => x.OrganizationUnit)
            .WithMany()
            .HasForeignKey(x => x.OrganizationUnitId)
            .OnDelete(DeleteBehavior.Restrict);

        b.HasQueryFilter(x => !x.IsDeleted);
    }
}

public class DailyWorkItemConfiguration : IEntityTypeConfiguration<DailyWorkItem>
{
    public void Configure(EntityTypeBuilder<DailyWorkItem> b)
    {
        b.Property(x => x.Title).HasMaxLength(250).IsRequired();
        b.Property(x => x.Description).HasMaxLength(4000);
        b.Property(x => x.TechnologiesUsed).HasMaxLength(1000);
        b.Property(x => x.Result).HasMaxLength(2000);
        b.Property(x => x.RepositoryUrl).HasMaxLength(500);

        b.ToTable(t => t.HasCheckConstraint("CK_DailyWorkItem_Duration", "[DurationMinutes] > 0 AND [DurationMinutes] <= 1440"));

        b.HasOne(x => x.DailyReport)
            .WithMany(r => r.WorkItems)
            .HasForeignKey(x => x.DailyReportId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasOne(x => x.Project)
            .WithMany()
            .HasForeignKey(x => x.ProjectId)
            .OnDelete(DeleteBehavior.NoAction);

        b.HasOne(x => x.ProjectTask)
            .WithMany()
            .HasForeignKey(x => x.ProjectTaskId)
            .OnDelete(DeleteBehavior.NoAction);

        b.HasQueryFilter(x => !x.IsDeleted);
    }
}

public class ProjectConfiguration : IEntityTypeConfiguration<Project>
{
    public void Configure(EntityTypeBuilder<Project> b)
    {
        b.Property(x => x.Name).HasMaxLength(200).IsRequired();
        b.Property(x => x.Description).HasMaxLength(4000);
        b.Property(x => x.RepositoryUrl).HasMaxLength(500);

        b.ToTable(t => t.HasCheckConstraint("CK_Project_Progress", "[ProgressPercentage] >= 0 AND [ProgressPercentage] <= 100"));
        b.HasIndex(x => x.Status);
        b.HasIndex(x => x.MentorUserId);

        b.HasOne(x => x.OrganizationUnit)
            .WithMany(d => d.Projects)
            .HasForeignKey(x => x.OrganizationUnitId)
            .OnDelete(DeleteBehavior.Restrict);

        b.HasQueryFilter(x => !x.IsDeleted);
    }
}

public class ProjectAssignmentConfiguration : IEntityTypeConfiguration<ProjectAssignment>
{
    public void Configure(EntityTypeBuilder<ProjectAssignment> b)
    {
        b.Property(x => x.RoleDescription).HasMaxLength(500);

        // Aynı stajyer aynı projeye iki kez AKTİF olarak atanamaz (filtered unique index).
        b.HasIndex(x => new { x.ProjectId, x.InternProfileId })
            .IsUnique()
            .HasFilter("[IsActive] = 1 AND [IsDeleted] = 0");

        b.HasOne(x => x.Project)
            .WithMany(p => p.Assignments)
            .HasForeignKey(x => x.ProjectId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasOne(x => x.InternProfile)
            .WithMany(i => i.ProjectAssignments)
            .HasForeignKey(x => x.InternProfileId)
            .OnDelete(DeleteBehavior.Restrict);

        b.HasQueryFilter(x => !x.IsDeleted);
    }
}

public class ProjectTaskConfiguration : IEntityTypeConfiguration<ProjectTask>
{
    public void Configure(EntityTypeBuilder<ProjectTask> b)
    {
        b.Property(x => x.Title).HasMaxLength(250).IsRequired();
        b.Property(x => x.Description).HasMaxLength(4000);
        b.HasIndex(x => x.Status);
        b.HasIndex(x => x.DueDate);

        b.HasOne(x => x.Project)
            .WithMany(p => p.Tasks)
            .HasForeignKey(x => x.ProjectId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasOne(x => x.AssignedInternProfile)
            .WithMany()
            .HasForeignKey(x => x.AssignedInternProfileId)
            .OnDelete(DeleteBehavior.NoAction);

        b.HasQueryFilter(x => !x.IsDeleted);
    }
}

public class LeaveRequestConfiguration : IEntityTypeConfiguration<LeaveRequest>
{
    public void Configure(EntityTypeBuilder<LeaveRequest> b)
    {
        b.Property(x => x.Reason).HasMaxLength(1000).IsRequired();
        b.Property(x => x.DocumentPath).HasMaxLength(500);
        b.Property(x => x.ReviewerNote).HasMaxLength(1000);
        b.HasIndex(x => x.Status);
        b.HasIndex(x => new { x.StartDate, x.EndDate });

        b.HasOne(x => x.InternshipPeriod)
            .WithMany(p => p.LeaveRequests)
            .HasForeignKey(x => x.InternshipPeriodId)
            .OnDelete(DeleteBehavior.Restrict);

        b.HasOne(x => x.OrganizationUnit)
            .WithMany()
            .HasForeignKey(x => x.OrganizationUnitId)
            .OnDelete(DeleteBehavior.Restrict);

        b.HasIndex(x => x.OrganizationUnitId);

        b.HasQueryFilter(x => !x.IsDeleted);
    }
}

public class EvaluationConfiguration : IEntityTypeConfiguration<Evaluation>
{
    public void Configure(EntityTypeBuilder<Evaluation> b)
    {
        b.Property(x => x.GeneralComment).HasMaxLength(2000);
        b.Ignore(x => x.AverageScore);

        b.ToTable(t =>
        {
            t.HasCheckConstraint("CK_Evaluation_Technical", "[TechnicalKnowledgeScore] BETWEEN 1 AND 5");
            t.HasCheckConstraint("CK_Evaluation_Responsibility", "[ResponsibilityScore] BETWEEN 1 AND 5");
            t.HasCheckConstraint("CK_Evaluation_Teamwork", "[TeamworkScore] BETWEEN 1 AND 5");
            t.HasCheckConstraint("CK_Evaluation_Communication", "[CommunicationScore] BETWEEN 1 AND 5");
            t.HasCheckConstraint("CK_Evaluation_ProblemSolving", "[ProblemSolvingScore] BETWEEN 1 AND 5");
            t.HasCheckConstraint("CK_Evaluation_TimeManagement", "[TimeManagementScore] BETWEEN 1 AND 5");
            t.HasCheckConstraint("CK_Evaluation_Attendance", "[AttendanceScore] BETWEEN 1 AND 5");
        });

        b.HasOne(x => x.InternshipPeriod)
            .WithMany(p => p.Evaluations)
            .HasForeignKey(x => x.InternshipPeriodId)
            .OnDelete(DeleteBehavior.Restrict);

        b.HasQueryFilter(x => !x.IsDeleted);
    }
}

public class AnnouncementConfiguration : IEntityTypeConfiguration<Announcement>
{
    public void Configure(EntityTypeBuilder<Announcement> b)
    {
        b.Property(x => x.Title).HasMaxLength(250).IsRequired();
        b.Property(x => x.Content).HasMaxLength(4000).IsRequired();
        b.HasIndex(x => x.PublishedAtUtc);
        b.HasQueryFilter(x => !x.IsDeleted);
    }
}

public class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> b)
    {
        b.Property(x => x.Title).HasMaxLength(250).IsRequired();
        b.Property(x => x.Message).HasMaxLength(2000).IsRequired();
        b.HasIndex(x => new { x.UserId, x.IsRead });
        b.HasQueryFilter(x => !x.IsDeleted);
    }
}

public class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> b)
    {
        b.Property(x => x.EntityName).HasMaxLength(150).IsRequired();
        b.Property(x => x.EntityId).HasMaxLength(100).IsRequired();
        b.Property(x => x.Action).HasMaxLength(100).IsRequired();
        b.Property(x => x.ActorNameSnapshot).HasMaxLength(200);
        b.Property(x => x.ActorRoleSnapshot).HasMaxLength(100);
        b.Property(x => x.SafeDescription).HasMaxLength(500);
        b.Property(x => x.IpAddress).HasMaxLength(64);
        b.Property(x => x.UserAgent).HasMaxLength(512);
        b.Property(x => x.RequestMethod).HasMaxLength(16);
        b.Property(x => x.RequestPath).HasMaxLength(500);
        b.Property(x => x.FailureReasonCode).HasMaxLength(100);
        b.Property(x => x.CorrelationId).HasMaxLength(64);

        b.HasIndex(x => new { x.EntityName, x.EntityId });
        b.HasIndex(x => x.CreatedAtUtc);
        b.HasIndex(x => x.ActorUserId);
        b.HasIndex(x => x.Action);
        b.HasIndex(x => x.OrganizationUnitId);
        b.HasIndex(x => x.IpAddress);
    }
}

public class StaffMessageConfiguration : IEntityTypeConfiguration<StaffMessage>
{
    public void Configure(EntityTypeBuilder<StaffMessage> b)
    {
        b.Property(x => x.Subject).HasMaxLength(200).IsRequired();
        b.Property(x => x.Body).HasMaxLength(4000).IsRequired();
        b.HasIndex(x => x.ThreadId);
        b.HasIndex(x => x.SenderUserId);
        b.HasIndex(x => x.RecipientUserId);
        b.HasIndex(x => x.SentAtUtc);
        b.HasIndex(x => new { x.RecipientUserId, x.IsRead });

        b.HasOne(x => x.OrganizationUnit)
            .WithMany()
            .HasForeignKey(x => x.OrganizationUnitId)
            .OnDelete(DeleteBehavior.Restrict);

        b.HasOne(x => x.ParentMessage)
            .WithMany()
            .HasForeignKey(x => x.ParentMessageId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class AiReportSummaryConfiguration : IEntityTypeConfiguration<AiReportSummary>
{
    public void Configure(EntityTypeBuilder<AiReportSummary> b)
    {
        b.Property(x => x.ExecutiveSummary).HasMaxLength(4000);
        b.Property(x => x.InputHash).HasMaxLength(128).IsRequired();
        b.Property(x => x.ModelName).HasMaxLength(100);
        b.Property(x => x.PromptVersion).HasMaxLength(20);
        b.Property(x => x.FailureReason).HasMaxLength(1000);
        b.HasIndex(x => x.InputHash);
        b.HasIndex(x => x.Status);

        b.HasOne(x => x.InternshipPeriod)
            .WithMany()
            .HasForeignKey(x => x.InternshipPeriodId)
            .OnDelete(DeleteBehavior.Restrict);

        b.HasQueryFilter(x => !x.IsDeleted);
    }
}
