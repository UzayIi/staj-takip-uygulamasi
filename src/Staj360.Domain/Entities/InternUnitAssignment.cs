using Staj360.Domain.Common;

namespace Staj360.Domain.Entities;

/// <summary>Stajyer birim ataması (tarihçeli). Transferde eski kayıt kapatılır, yenisi açılır.</summary>
public class InternUnitAssignment : AuditableEntity
{
    public Guid InternProfileId { get; set; }
    public InternProfile? InternProfile { get; set; }

    public Guid OrganizationUnitId { get; set; }
    public OrganizationUnit? OrganizationUnit { get; set; }

    /// <summary>Bu atama dönemindeki sorumlu danışman.</summary>
    public Guid AdvisorUserId { get; set; }

    public DateOnly StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
    public bool IsActive { get; set; } = true;
}
