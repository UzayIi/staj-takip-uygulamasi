using Staj360.Application.Common;

namespace Staj360.Application.Services.Accounts;

public record CreateInternRequest(
    string Email,
    string FullName,
    string Password,
    string StudentNumber,
    Guid OrganizationUnitId,
    Guid AdvisorUserId,
    string? NationalId,
    string? University,
    string? Faculty,
    string? SchoolDepartment,
    string? ClassLevel,
    string? PhoneNumber,
    string? Address,
    string? EmergencyContactName,
    string? EmergencyContactPhone);

public record CreateStaffRequest(string Email, string FullName, string Password, string Role);

public record AccountSummary(Guid UserId, string Email, string FullName, bool IsActive, IReadOnlyList<string> Roles);

/// <summary>
/// Identity hesap yönetimi. Herkese açık kayıt yoktur; hesapları Admin/SuperAdmin oluşturur.
/// Identity'ye bağımlı olduğu için implementasyonu Infrastructure katmanındadır.
/// </summary>
public interface IUserAccountService
{
    Task<ServiceResult<Guid>> CreateInternAsync(CreateInternRequest request, CancellationToken cancellationToken = default);
    Task<ServiceResult> DeleteInternAsync(Guid internProfileId, CancellationToken cancellationToken = default);
    Task<ServiceResult<Guid>> CreateStaffAsync(CreateStaffRequest request, CancellationToken cancellationToken = default);
    Task<ServiceResult> DeleteStaffAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<ServiceResult> SetActiveAsync(Guid userId, bool isActive, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AccountSummary>> ListByRoleAsync(string role, CancellationToken cancellationToken = default);
    Task<AccountSummary?> GetAsync(Guid userId, CancellationToken cancellationToken = default);
}
