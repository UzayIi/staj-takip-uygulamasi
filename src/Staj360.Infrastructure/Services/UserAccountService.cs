using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Staj360.Application.Abstractions;
using Staj360.Application.Common;
using Staj360.Application.Services.Accounts;
using Staj360.Domain.Entities;
using Staj360.Infrastructure.Identity;

namespace Staj360.Infrastructure.Services;

public class UserAccountService : IUserAccountService
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IApplicationDbContext _db;
    private readonly IClock _clock;
    private readonly IAuditLogService _audit;

    public UserAccountService(
        UserManager<ApplicationUser> userManager,
        IApplicationDbContext db,
        IClock clock,
        IAuditLogService audit)
    {
        _userManager = userManager;
        _db = db;
        _clock = clock;
        _audit = audit;
    }

    public async Task<ServiceResult<Guid>> CreateInternAsync(CreateInternRequest request, CancellationToken cancellationToken = default)
    {
        var existing = await _userManager.FindByEmailAsync(request.Email);
        if (existing is not null)
            return ServiceResult<Guid>.Fail("Bu e-posta ile bir kullanıcı zaten var.", "DUPLICATE_EMAIL");

        var user = new ApplicationUser
        {
            UserName = request.Email,
            Email = request.Email,
            FullName = request.FullName,
            IsActive = true,
            CreatedAtUtc = _clock.UtcNow,
            EmailConfirmed = true
        };

        var create = await _userManager.CreateAsync(user, request.Password);
        if (!create.Succeeded)
            return ServiceResult<Guid>.Invalid(create.Errors.Select(e => e.Description));

        await _userManager.AddToRoleAsync(user, AppRoles.Intern);

        var profile = new InternProfile
        {
            UserId = user.Id,
            StudentNumber = request.StudentNumber,
            NationalId = request.NationalId,
            University = request.University,
            Faculty = request.Faculty,
            SchoolDepartment = request.SchoolDepartment,
            ClassLevel = request.ClassLevel,
            PhoneNumber = request.PhoneNumber,
            EmergencyContactName = request.EmergencyContactName,
            EmergencyContactPhone = request.EmergencyContactPhone,
            DepartmentId = request.DepartmentId,
            IsActive = true
        };

        try
        {
            _db.InternProfiles.Add(profile);
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch
        {
            // Profil kaydı başarısızsa oluşturulan kullanıcıyı geri al.
            await _userManager.DeleteAsync(user);
            return ServiceResult<Guid>.Fail("Stajyer profili oluşturulamadı.", "PROFILE_FAILED");
        }

        // Not: T.C. kimlik numarası gibi hassas veriler audit log'a yazılmaz.
        await _audit.LogAsync(nameof(ApplicationUser), user.Id.ToString(), "CreateIntern", cancellationToken: cancellationToken);
        return ServiceResult<Guid>.Ok(user.Id);
    }

    public async Task<ServiceResult> DeleteInternAsync(Guid internProfileId, CancellationToken cancellationToken = default)
    {
        var profile = await _db.InternProfiles.FirstOrDefaultAsync(p => p.Id == internProfileId, cancellationToken);
        if (profile is null || profile.IsDeleted)
            return ServiceResult.Fail("Stajyer bulunamadı.", "NOT_FOUND");
        
        profile.IsDeleted = true;
        
        // Stajyere ait Identity kullanıcısını pasife alıyoruz (soft-delete benzeri davranış)
        var user = await _userManager.FindByIdAsync(profile.UserId.ToString());
        if (user is not null)
        {
            user.IsActive = false;
            await _userManager.UpdateAsync(user);
        }

        await _db.SaveChangesAsync(cancellationToken);
        await _audit.LogAsync(nameof(InternProfile), internProfileId.ToString(), "Delete", cancellationToken: cancellationToken);
        
        return ServiceResult.Ok();
    }

    public async Task<ServiceResult<Guid>> CreateStaffAsync(CreateStaffRequest request, CancellationToken cancellationToken = default)
    {
        if (request.Role is not (AppRoles.Admin or AppRoles.Mentor or AppRoles.SuperAdmin))
            return ServiceResult<Guid>.Fail("Geçersiz rol.", "INVALID_ROLE");

        var existing = await _userManager.FindByEmailAsync(request.Email);
        if (existing is not null)
            return ServiceResult<Guid>.Fail("Bu e-posta ile bir kullanıcı zaten var.", "DUPLICATE_EMAIL");

        var user = new ApplicationUser
        {
            UserName = request.Email,
            Email = request.Email,
            FullName = request.FullName,
            IsActive = true,
            CreatedAtUtc = _clock.UtcNow,
            EmailConfirmed = true
        };

        var create = await _userManager.CreateAsync(user, request.Password);
        if (!create.Succeeded)
            return ServiceResult<Guid>.Invalid(create.Errors.Select(e => e.Description));

        await _userManager.AddToRoleAsync(user, request.Role);
        await _audit.LogAsync(nameof(ApplicationUser), user.Id.ToString(), $"CreateStaff:{request.Role}", cancellationToken: cancellationToken);
        return ServiceResult<Guid>.Ok(user.Id);
    }

    public async Task<ServiceResult> DeleteStaffAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user is null)
            return ServiceResult.Fail("Kullanıcı bulunamadı.", "NOT_FOUND");
            
        var roles = await _userManager.GetRolesAsync(user);
        if (roles.Contains(AppRoles.SuperAdmin))
            return ServiceResult.Fail("Süper admin hesabı silinemez.", "FORBIDDEN");

        var result = await _userManager.DeleteAsync(user);
        if (!result.Succeeded)
            return ServiceResult.Fail("Kullanıcı silinirken bir hata oluştu.", "DELETE_FAILED");
        
        await _audit.LogAsync(nameof(ApplicationUser), userId.ToString(), "DeleteStaff", cancellationToken: cancellationToken);
        return ServiceResult.Ok();
    }

    public async Task<ServiceResult> SetActiveAsync(Guid userId, bool isActive, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user is null)
            return ServiceResult.Fail("Kullanıcı bulunamadı.", "NOT_FOUND");

        user.IsActive = isActive;
        await _userManager.UpdateAsync(user);
        await _audit.LogAsync(nameof(ApplicationUser), userId.ToString(), isActive ? "Activate" : "Deactivate", cancellationToken: cancellationToken);
        return ServiceResult.Ok();
    }

    public async Task<IReadOnlyList<AccountSummary>> ListByRoleAsync(string role, CancellationToken cancellationToken = default)
    {
        var users = await _userManager.GetUsersInRoleAsync(role);
        var result = new List<AccountSummary>();
        foreach (var u in users.OrderBy(u => u.FullName))
        {
            var roles = await _userManager.GetRolesAsync(u);
            result.Add(new AccountSummary(u.Id, u.Email ?? string.Empty, u.FullName, u.IsActive, roles.ToList()));
        }
        return result;
    }

    public async Task<AccountSummary?> GetAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user is null) return null;
        var roles = await _userManager.GetRolesAsync(user);
        return new AccountSummary(user.Id, user.Email ?? string.Empty, user.FullName, user.IsActive, roles.ToList());
    }
}
