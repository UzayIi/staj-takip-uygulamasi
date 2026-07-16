using Staj360.Domain.Enums;

namespace Staj360.Application.Services.Internships;

/// <summary>Admin stajyer listesi satırı. Ad Soyad Identity'deki FullName alanından gelir.</summary>
public class InternListItemDto
{
    public Guid InternProfileId { get; init; }
    public Guid UserId { get; init; }
    public string FullName { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public string StudentNumber { get; init; } = string.Empty;
    public string? University { get; init; }
    public string? SchoolDepartment { get; init; }
    public string DepartmentName { get; init; } = string.Empty;
    public string? MentorFullName { get; init; }
    public DateOnly? PeriodStartDate { get; init; }
    public DateOnly? PeriodEndDate { get; init; }
    public InternshipStatus? PeriodStatus { get; init; }
    public bool IsActive { get; init; }
}
