using Microsoft.EntityFrameworkCore;
using Staj360.Application.Abstractions;
using Staj360.Application.Common;
using Staj360.Domain.Entities;

namespace Staj360.Application.Services.Internships;

public class WorkScheduleService : IWorkScheduleService
{
    private readonly IApplicationDbContext _db;

    public WorkScheduleService(IApplicationDbContext db) => _db = db;

    public async Task<IReadOnlyList<WorkSchedule>> ListAsync(CancellationToken cancellationToken = default) =>
        await _db.WorkSchedules.AsNoTracking().Where(w => !w.IsDeleted).OrderBy(w => w.Name).ToListAsync(cancellationToken);

    public async Task<WorkSchedule?> GetAsync(Guid id, CancellationToken cancellationToken = default) =>
        await _db.WorkSchedules.FirstOrDefaultAsync(w => w.Id == id && !w.IsDeleted, cancellationToken);

    public async Task<ServiceResult<WorkSchedule>> CreateAsync(WorkScheduleInput input, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(input.Name))
            return ServiceResult<WorkSchedule>.Fail("Program adı zorunludur.", "VALIDATION");
        if (input.EndTime <= input.StartTime)
            return ServiceResult<WorkSchedule>.Fail("Bitiş saati başlangıç saatinden sonra olmalıdır.", "VALIDATION");
        if (input.GracePeriodMinutes < 0)
            return ServiceResult<WorkSchedule>.Fail("Tolerans süresi negatif olamaz.", "VALIDATION");

        var entity = new WorkSchedule
        {
            Name = input.Name.Trim(),
            StartTime = input.StartTime,
            EndTime = input.EndTime,
            GracePeriodMinutes = input.GracePeriodMinutes,
            MondayEnabled = input.Monday,
            TuesdayEnabled = input.Tuesday,
            WednesdayEnabled = input.Wednesday,
            ThursdayEnabled = input.Thursday,
            FridayEnabled = input.Friday,
            SaturdayEnabled = input.Saturday,
            SundayEnabled = input.Sunday
        };
        _db.WorkSchedules.Add(entity);
        await _db.SaveChangesAsync(cancellationToken);
        return ServiceResult<WorkSchedule>.Ok(entity);
    }
}
