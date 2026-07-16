using Staj360.Domain.Common;

namespace Staj360.Domain.Entities;

public class Department : AuditableEntity
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;

    public ICollection<InternProfile> Interns { get; set; } = new List<InternProfile>();
    public ICollection<Project> Projects { get; set; } = new List<Project>();
}
