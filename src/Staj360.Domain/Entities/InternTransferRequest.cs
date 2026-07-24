using Staj360.Domain.Common;
using Staj360.Domain.Enums;

namespace Staj360.Domain.Entities;

/// <summary>Müdürlükler arası stajyer transfer talebi.</summary>
public class InternTransferRequest : AuditableEntity
{
    public Guid InternProfileId { get; set; }
    public InternProfile? InternProfile { get; set; }

    public Guid SourceOrganizationUnitId { get; set; }
    public OrganizationUnit? SourceOrganizationUnit { get; set; }

    public Guid TargetOrganizationUnitId { get; set; }
    public OrganizationUnit? TargetOrganizationUnit { get; set; }

    public Guid RequestedByUserId { get; set; }

    public TransferRequestStatus Status { get; set; } = TransferRequestStatus.Pending;

    /// <summary>Kaynak yöneticinin planladığı yeni görevlendirme başlangıç tarihi.</summary>
    public DateOnly? PlannedStartDate { get; set; }

    /// <summary>Onay sırasında seçilen hedef danışman (onayda zorunlu).</summary>
    public Guid? TargetAdvisorUserId { get; set; }

    public Guid? DecisionByUserId { get; set; }
    public DateTime? DecisionAtUtc { get; set; }
    public string? RequestNote { get; set; }
    public string? DecisionNote { get; set; }
}
