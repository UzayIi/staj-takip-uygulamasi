using Staj360.Domain.Entities;

namespace Staj360.Web.Areas.Admin.Models;

public class AuditLogFilterViewModel
{
    public Guid? UserId { get; set; }
    public string? Role { get; set; }
    public string? Action { get; set; }
    public Guid? OrganizationUnitId { get; set; }
    public DateTime? FromUtc { get; set; }
    public DateTime? ToUtc { get; set; }
    public string? IpAddress { get; set; }
    public bool? IsSuccessful { get; set; }
    public string? Search { get; set; }
    public int Page { get; set; } = 1;
    public int TotalCount { get; set; }
    public int TotalPages { get; set; } = 1;
    public IReadOnlyList<AuditLog> Items { get; set; } = Array.Empty<AuditLog>();
}
