using Microsoft.EntityFrameworkCore;
using Staj360.Application.Abstractions;
using Staj360.Application.Common;
using Staj360.Domain.Entities;
using Staj360.Domain.Enums;

namespace Staj360.Application.Services.Internships;

public class InternshipPeriodService : IInternshipPeriodService
{
    private readonly IApplicationDbContext _db;
    private readonly IAuditLogService _audit;

    public InternshipPeriodService(IApplicationDbContext db, IAuditLogService audit)
    {
        _db = db;
        _audit = audit;
    }

    public async Task<PagedResult<InternshipPeriod>> ListAsync(int page, int pageSize, CancellationToken cancellationToken = default)
    {
        page = page < 1 ? 1 : page;
        pageSize = pageSize is < 1 or > 200 ? 20 : pageSize;

        var query = _db.InternshipPeriods.AsNoTracking()
            .Include(p => p.InternProfile)
            .Include(p => p.WorkSchedule)
            .Where(p => !p.IsDeleted);

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(p => p.StartDate)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<InternshipPeriod> { Items = items, TotalCount = total, Page = page, PageSize = pageSize };
    }

    public async Task<InternshipPeriod?> GetAsync(Guid id, CancellationToken cancellationToken = default) =>
        await _db.InternshipPeriods
            .Include(p => p.InternProfile)
            .Include(p => p.WorkSchedule)
            .FirstOrDefaultAsync(p => p.Id == id && !p.IsDeleted, cancellationToken);

    public async Task<ServiceResult<InternshipPeriod>> CreateAsync(CreateInternshipPeriodCommand command, CancellationToken cancellationToken = default)
    {
        if (command.EndDate < command.StartDate)
            return ServiceResult<InternshipPeriod>.Fail("Bitiş tarihi başlangıç tarihinden önce olamaz.", "VALIDATION");

        var internExists = await _db.InternProfiles.AnyAsync(i => i.Id == command.InternProfileId && !i.IsDeleted, cancellationToken);
        if (!internExists)
            return ServiceResult<InternshipPeriod>.Fail("Stajyer profili bulunamadı.", "NOT_FOUND");

        var scheduleExists = await _db.WorkSchedules.AnyAsync(w => w.Id == command.WorkScheduleId && !w.IsDeleted, cancellationToken);
        if (!scheduleExists)
            return ServiceResult<InternshipPeriod>.Fail("Çalışma programı bulunamadı.", "NOT_FOUND");

        var entity = new InternshipPeriod
        {
            InternProfileId = command.InternProfileId,
            MentorUserId = command.MentorUserId,
            WorkScheduleId = command.WorkScheduleId,
            StartDate = command.StartDate,
            EndDate = command.EndDate,
            RequiredWorkDays = command.RequiredWorkDays,
            Status = InternshipStatus.Pending
        };
        _db.InternshipPeriods.Add(entity);
        await _db.SaveChangesAsync(cancellationToken);
        await _audit.LogAsync(nameof(InternshipPeriod), entity.Id.ToString(), "Create", cancellationToken: cancellationToken);
        return ServiceResult<InternshipPeriod>.Ok(entity);
    }

    public async Task<ServiceResult> ActivateAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _db.InternshipPeriods.FirstOrDefaultAsync(p => p.Id == id && !p.IsDeleted, cancellationToken);
        if (entity is null)
            return ServiceResult.Fail("Staj dönemi bulunamadı.", "NOT_FOUND");

        entity.Status = InternshipStatus.Active;
        await _db.SaveChangesAsync(cancellationToken);
        await _audit.LogAsync(nameof(InternshipPeriod), entity.Id.ToString(), "Activate", cancellationToken: cancellationToken);
        return ServiceResult.Ok();
    }

    public async Task<ServiceResult> CompleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _db.InternshipPeriods.FirstOrDefaultAsync(p => p.Id == id && !p.IsDeleted, cancellationToken);
        if (entity is null)
            return ServiceResult.Fail("Staj dönemi bulunamadı.", "NOT_FOUND");

        entity.Status = InternshipStatus.Completed;
        await _db.SaveChangesAsync(cancellationToken);
        await _audit.LogAsync(nameof(InternshipPeriod), entity.Id.ToString(), "Complete", cancellationToken: cancellationToken);
        return ServiceResult.Ok();
    }

    public async Task<IReadOnlyList<InternshipPeriod>> ListForMentorAsync(Guid mentorUserId, CancellationToken cancellationToken = default) =>
        await _db.InternshipPeriods.AsNoTracking()
            .Include(p => p.InternProfile)
            .Where(p => p.MentorUserId == mentorUserId && !p.IsDeleted)
            .OrderByDescending(p => p.StartDate)
            .ToListAsync(cancellationToken);

    public async Task<InternshipPeriod?> GetActiveForUserAsync(Guid userId, CancellationToken cancellationToken = default) =>
        await _db.InternshipPeriods.AsNoTracking()
            .Include(p => p.InternProfile)
            .Include(p => p.WorkSchedule)
            .FirstOrDefaultAsync(p => p.InternProfile!.UserId == userId && p.Status == InternshipStatus.Active && !p.IsDeleted, cancellationToken);
}
