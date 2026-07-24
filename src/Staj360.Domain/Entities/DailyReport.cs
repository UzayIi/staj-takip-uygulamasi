using Staj360.Domain.Common;
using Staj360.Domain.Enums;

namespace Staj360.Domain.Entities;

public class DailyReport : AuditableEntity
{
    public Guid InternshipPeriodId { get; set; }
    public InternshipPeriod? InternshipPeriod { get; set; }

    /// <summary>İstanbul takvim günü. Oluşturulduktan sonra değiştirilmez.</summary>
    public DateOnly ReportDate { get; set; }

    /// <summary>
    /// Rapor oluşturulurken stajyerin aktif müdürlük atamasının birim kimliği (snapshot).
    /// Transfer sonrası eski raporların birim bilgisi korunur.
    /// </summary>
    public Guid OrganizationUnitId { get; set; }
    public OrganizationUnit? OrganizationUnit { get; set; }

    public string? GeneralNotes { get; set; }
    public string? ProblemsEncountered { get; set; }
    public string? SolutionsApplied { get; set; }
    public string? TomorrowPlan { get; set; }

    public DailyReportStatus Status { get; set; } = DailyReportStatus.Draft;
    public DateTime? SubmittedAtUtc { get; set; }
    public DateTime? ReviewedAtUtc { get; set; }
    public Guid? ReviewedByUserId { get; set; }
    public string? MentorComment { get; set; }

    public ICollection<DailyWorkItem> WorkItems { get; set; } = new List<DailyWorkItem>();
}
