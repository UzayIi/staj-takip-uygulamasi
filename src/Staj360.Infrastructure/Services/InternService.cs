using Microsoft.EntityFrameworkCore;
using Staj360.Application.Common;
using Staj360.Application.Services.Internships;
using Staj360.Domain.Entities;
using Staj360.Domain.Enums;
using Staj360.Infrastructure.Persistence;

namespace Staj360.Infrastructure.Services;

/// <summary>
/// Stajyer listeleme Identity (FullName/Email) ile tek sorguda birleştirilir.
/// Application InternService yerine bu sınıf IInternService olarak kaydedilir.
/// </summary>
public class InternService : IInternService
{
    private readonly AppDbContext _db;

    public InternService(AppDbContext db) => _db = db;

    public async Task<PagedResult<InternListItemDto>> ListAsync(
        string? search,
        int? year,
        DateOnly? startDate,
        DateOnly? endDate,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        page = page < 1 ? 1 : page;
        pageSize = pageSize is < 1 or > 200 ? 20 : pageSize;

        // Aktif veya en son dönem bilgisi + mentor adı tek sorguda.
        var query =
            from i in _db.InternProfiles.AsNoTracking()
            join u in _db.Users.AsNoTracking() on i.UserId equals u.Id
            join d in _db.OrganizationUnits.AsNoTracking() on i.CurrentOrganizationUnitId equals d.Id
            where !i.IsDeleted
            select new { Intern = i, User = u, Department = d };

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(x =>
                x.User.FullName.Contains(term) ||
                (x.User.Email != null && x.User.Email.Contains(term)) ||
                x.Intern.StudentNumber.Contains(term) ||
                (x.Intern.University != null && x.Intern.University.Contains(term)) ||
                (x.Intern.SchoolDepartment != null && x.Intern.SchoolDepartment.Contains(term)) ||
                x.Department.Name.Contains(term));
        }

        // Yıl filtresi: staj döneminin başladığı yıla göre filtreler.
        if (year.HasValue)
        {
            var internIdsInYear = _db.InternshipPeriods.AsNoTracking()
                .Where(p => !p.IsDeleted && p.StartDate.Year == year.Value)
                .Select(p => p.InternProfileId)
                .Distinct();
            query = query.Where(x => internIdsInYear.Contains(x.Intern.Id));
        }

        // Tarih aralığı: staj dönemi seçilen aralıkla kesişen stajyerler (yerel DateOnly).
        if (startDate.HasValue || endDate.HasValue)
        {
            var overlappingPeriods = _db.InternshipPeriods.AsNoTracking().Where(p => !p.IsDeleted);

            if (startDate.HasValue && endDate.HasValue)
            {
                var from = startDate.Value;
                var to = endDate.Value;
                overlappingPeriods = overlappingPeriods.Where(p => p.StartDate <= to && p.EndDate >= from);
            }
            else if (startDate.HasValue)
            {
                var from = startDate.Value;
                overlappingPeriods = overlappingPeriods.Where(p => p.EndDate >= from);
            }
            else
            {
                var to = endDate!.Value;
                overlappingPeriods = overlappingPeriods.Where(p => p.StartDate <= to);
            }

            var internIdsInRange = overlappingPeriods.Select(p => p.InternProfileId).Distinct();
            query = query.Where(x => internIdsInRange.Contains(x.Intern.Id));
        }

        var total = await query.CountAsync(cancellationToken);

        var pageRows = await query
            .OrderBy(x => x.User.FullName)
            .ThenBy(x => x.Intern.StudentNumber)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new
            {
                x.Intern.Id,
                x.Intern.UserId,
                FullName = x.User.FullName,
                Email = x.User.Email ?? string.Empty,
                x.Intern.StudentNumber,
                x.Intern.NationalId,
                x.Intern.University,
                x.Intern.Faculty,
                x.Intern.SchoolDepartment,
                x.Intern.ClassLevel,
                x.Intern.PhoneNumber,
                x.Intern.Address,
                x.Intern.EmergencyContactName,
                x.Intern.EmergencyContactPhone,
                x.Intern.CurrentOrganizationUnitId,
                DepartmentName = x.Department.Name,
                x.Intern.IsActive
            })
            .ToListAsync(cancellationToken);

        var profileIds = pageRows.Select(r => r.Id).ToList();
        var periods = await _db.InternshipPeriods.AsNoTracking()
            .Where(p => profileIds.Contains(p.InternProfileId) && !p.IsDeleted)
            .OrderByDescending(p => p.Status == InternshipStatus.Active)
            .ThenByDescending(p => p.StartDate)
            .ToListAsync(cancellationToken);

        var mentorIds = periods.Select(p => p.MentorUserId).Distinct().ToList();
        var mentors = await _db.Users.AsNoTracking()
            .Where(u => mentorIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.FullName, cancellationToken);

        var periodByIntern = periods
            .GroupBy(p => p.InternProfileId)
            .ToDictionary(g => g.Key, g => g.First());

        var items = pageRows.Select(r =>
        {
            periodByIntern.TryGetValue(r.Id, out var period);
            string? mentorName = null;
            if (period is not null)
                mentors.TryGetValue(period.MentorUserId, out mentorName);

            return new InternListItemDto
            {
                InternProfileId = r.Id,
                UserId = r.UserId,
                FullName = string.IsNullOrWhiteSpace(r.FullName) ? "(İsimsiz)" : r.FullName,
                Email = r.Email,
                StudentNumber = r.StudentNumber,
                NationalIdMasked = MaskNationalId(r.NationalId),
                University = r.University,
                Faculty = r.Faculty,
                SchoolDepartment = r.SchoolDepartment,
                ClassLevel = r.ClassLevel,
                PhoneNumber = r.PhoneNumber,
                Address = r.Address,
                EmergencyContactName = r.EmergencyContactName,
                EmergencyContactPhone = r.EmergencyContactPhone,
                OrganizationUnitId = r.CurrentOrganizationUnitId,
                OrganizationUnitName = r.DepartmentName,
                MentorFullName = mentorName,
                PeriodStartDate = period?.StartDate,
                PeriodEndDate = period?.EndDate,
                PeriodStatus = period?.Status,
                IsActive = r.IsActive
            };
        }).ToList();

        return new PagedResult<InternListItemDto>
        {
            Items = items,
            TotalCount = total,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<InternProfile?> GetAsync(Guid id, CancellationToken cancellationToken = default) =>
        await _db.InternProfiles.Include(i => i.CurrentOrganizationUnit).FirstOrDefaultAsync(i => i.Id == id && !i.IsDeleted, cancellationToken);

    public async Task<InternProfile?> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default) =>
        await _db.InternProfiles.Include(i => i.CurrentOrganizationUnit).FirstOrDefaultAsync(i => i.UserId == userId && !i.IsDeleted, cancellationToken);

    public async Task<IReadOnlyList<InternProfile>> ListForMentorAsync(Guid mentorUserId, CancellationToken cancellationToken = default)
    {
        var internIds = await _db.InternshipPeriods.AsNoTracking()
            .Where(p => p.MentorUserId == mentorUserId && !p.IsDeleted)
            .Select(p => p.InternProfileId)
            .Distinct()
            .ToListAsync(cancellationToken);

        return await _db.InternProfiles.AsNoTracking()
            .Include(i => i.CurrentOrganizationUnit)
            .Where(i => internIds.Contains(i.Id) && !i.IsDeleted)
            .OrderBy(i => i.StudentNumber)
            .ToListAsync(cancellationToken);
    }

    public async Task<int> CountActiveAsync(CancellationToken cancellationToken = default) =>
        await _db.InternProfiles.AsNoTracking().CountAsync(i => i.IsActive && !i.IsDeleted, cancellationToken);

    /// <summary>T.C. Kimlik numarasını maskeler: ilk 3 ve son 2 hane gösterilir.</summary>
    private static string? MaskNationalId(string? nationalId)
    {
        if (string.IsNullOrWhiteSpace(nationalId)) return null;
        if (nationalId.Length <= 5) return new string('*', nationalId.Length);
        return string.Concat(nationalId.AsSpan(0, 3), new string('*', nationalId.Length - 5), nationalId.AsSpan(nationalId.Length - 2));
    }
}

