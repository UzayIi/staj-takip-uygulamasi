using Microsoft.EntityFrameworkCore;
using Staj360.Application.Abstractions;
using Staj360.Application.Common;
using Staj360.Domain.Entities;
using Staj360.Domain.Enums;

namespace Staj360.Application.Services.Attendance;

/// <summary>
/// Giriş-çıkış iş kurallarını uygular. Zamanlar sunucudan (IClock) alınır; iş günü
/// yerel tarihe göre belirlenir. Stajyer zamanları doğrudan düzenleyemez.
/// </summary>
public class AttendanceService : IAttendanceService
{
    private readonly IApplicationDbContext _db;
    private readonly IClock _clock;
    private readonly ITimeZoneService _tz;

    public AttendanceService(IApplicationDbContext db, IClock clock, ITimeZoneService tz)
    {
        _db = db;
        _clock = clock;
        _tz = tz;
    }

    public async Task<ServiceResult<AttendanceDaySummary>> CheckInAsync(Guid userId, AttendanceActionContext context, CancellationToken cancellationToken = default)
    {
        var period = await GetActivePeriodAsync(userId, cancellationToken);
        if (period is null)
            return ServiceResult<AttendanceDaySummary>.Fail("Aktif bir staj döneminiz bulunmadığı için giriş yapamazsınız.", "NO_ACTIVE_PERIOD");

        var now = _clock.UtcNow;
        var workDate = _tz.LocalDate(now);

        if (period.WorkSchedule is not null && !period.WorkSchedule.IsWorkingDay(workDate.DayOfWeek))
            return ServiceResult<AttendanceDaySummary>.Fail("Bugün çalışma programınıza dahil bir gün değil, giriş yapılamaz.", "NOT_WORKDAY");

        var onLeave = await _db.LeaveRequests.AnyAsync(l =>
            l.InternshipPeriodId == period.Id &&
            l.Status == LeaveRequestStatus.Approved &&
            l.StartDate <= workDate && l.EndDate >= workDate &&
            !l.IsDeleted, cancellationToken);
        if (onLeave)
            return ServiceResult<AttendanceDaySummary>.Fail("Bu tarihte onaylı izniniz bulunduğu için giriş yapılamaz.", "ON_LEAVE");

        var day = await GetOrCreateDayAsync(period, workDate, cancellationToken);

        if (HasOpenCheckIn(day))
            return ServiceResult<AttendanceDaySummary>.Fail("Zaten açık bir giriş kaydınız var. Önce çıkış yapmalısınız.", "ALREADY_CHECKED_IN");

        var checkIn = new AttendanceEvent
        {
            AttendanceDayId = day.Id,
            EventType = AttendanceEventType.CheckIn,
            EventTimeUtc = now,
            Source = context.Source,
            IpAddress = context.IpAddress,
            DeviceInfo = context.DeviceInfo
        };
        day.Events.Add(checkIn);
        _db.AttendanceEvents.Add(checkIn);

        Recalculate(day, period);
        await _db.SaveChangesAsync(cancellationToken);
        return ServiceResult<AttendanceDaySummary>.Ok(ToSummary(day));
    }

    public async Task<ServiceResult<AttendanceDaySummary>> CheckOutAsync(Guid userId, AttendanceActionContext context, CancellationToken cancellationToken = default)
    {
        var period = await GetActivePeriodAsync(userId, cancellationToken);
        if (period is null)
            return ServiceResult<AttendanceDaySummary>.Fail("Aktif bir staj döneminiz bulunmadığı için çıkış yapamazsınız.", "NO_ACTIVE_PERIOD");

        var now = _clock.UtcNow;
        var workDate = _tz.LocalDate(now);

        var day = await _db.AttendanceDays
            .Include(d => d.Events)
            .FirstOrDefaultAsync(d => d.InternshipPeriodId == period.Id && d.WorkDate == workDate && !d.IsDeleted, cancellationToken);

        if (day is null || !HasOpenCheckIn(day))
            return ServiceResult<AttendanceDaySummary>.Fail("Açık bir giriş kaydınız olmadan çıkış yapamazsınız.", "NOT_CHECKED_IN");

        var checkOut = new AttendanceEvent
        {
            AttendanceDayId = day.Id,
            EventType = AttendanceEventType.CheckOut,
            EventTimeUtc = now,
            Source = context.Source,
            IpAddress = context.IpAddress,
            DeviceInfo = context.DeviceInfo
        };
        day.Events.Add(checkOut);
        _db.AttendanceEvents.Add(checkOut);

        Recalculate(day, period);
        await _db.SaveChangesAsync(cancellationToken);
        return ServiceResult<AttendanceDaySummary>.Ok(ToSummary(day));
    }

    public async Task<ServiceResult<AttendanceDaySummary>> GetTodayStatusAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var period = await GetActivePeriodAsync(userId, cancellationToken);
        if (period is null)
            return ServiceResult<AttendanceDaySummary>.Fail("Aktif bir staj döneminiz bulunmuyor.", "NO_ACTIVE_PERIOD");

        var workDate = _tz.LocalDate(_clock.UtcNow);
        var day = await _db.AttendanceDays
            .AsNoTracking()
            .Include(d => d.Events)
            .FirstOrDefaultAsync(d => d.InternshipPeriodId == period.Id && d.WorkDate == workDate && !d.IsDeleted, cancellationToken);

        if (day is null)
        {
            return ServiceResult<AttendanceDaySummary>.Ok(new AttendanceDaySummary(
                workDate, null, null, 0, AttendanceStatus.NotStarted, false, false, false, false));
        }

        return ServiceResult<AttendanceDaySummary>.Ok(ToSummary(day));
    }

    private async Task<InternshipPeriod?> GetActivePeriodAsync(Guid userId, CancellationToken cancellationToken)
    {
        var today = _tz.LocalDate(_clock.UtcNow);
        return await _db.InternshipPeriods
            .AsNoTracking()
            .Include(p => p.WorkSchedule)
            .Include(p => p.InternProfile)
            .FirstOrDefaultAsync(p =>
                p.InternProfile!.UserId == userId &&
                p.Status == InternshipStatus.Active &&
                p.StartDate <= today && p.EndDate >= today &&
                !p.IsDeleted, cancellationToken);
    }

    private async Task<AttendanceDay> GetOrCreateDayAsync(InternshipPeriod period, DateOnly workDate, CancellationToken cancellationToken)
    {
        var day = await _db.AttendanceDays
            .Include(d => d.Events)
            .FirstOrDefaultAsync(d => d.InternshipPeriodId == period.Id && d.WorkDate == workDate && !d.IsDeleted, cancellationToken);

        if (day is null)
        {
            day = new AttendanceDay
            {
                InternshipPeriodId = period.Id,
                WorkDate = workDate,
                Status = AttendanceStatus.NotStarted
            };
            _db.AttendanceDays.Add(day);
        }

        return day;
    }

    // Açık giriş: CheckIn sayısı CheckOut sayısından fazla ise son çift kapanmamış demektir.
    private static bool HasOpenCheckIn(AttendanceDay day)
    {
        var checkIns = day.Events.Count(e => e.EventType == AttendanceEventType.CheckIn);
        var checkOuts = day.Events.Count(e => e.EventType == AttendanceEventType.CheckOut);
        return checkIns > checkOuts;
    }

    /// <summary>
    /// İlk giriş, son çıkış, toplam eşleştirilmiş süre, geç kalma ve eksik durumunu hesaplar.
    /// Bir gün içinde birden fazla giriş-çıkış çifti desteklenir.
    /// </summary>
    private void Recalculate(AttendanceDay day, InternshipPeriod period)
    {
        var ordered = day.Events.OrderBy(e => e.EventTimeUtc).ToList();

        DateTime? firstIn = ordered.FirstOrDefault(e => e.EventType == AttendanceEventType.CheckIn)?.EventTimeUtc;
        DateTime? lastOut = ordered.LastOrDefault(e => e.EventType == AttendanceEventType.CheckOut)?.EventTimeUtc;

        int totalMinutes = 0;
        DateTime? openIn = null;
        foreach (var e in ordered)
        {
            if (e.EventType == AttendanceEventType.CheckIn)
            {
                openIn ??= e.EventTimeUtc;
            }
            else if (e.EventType == AttendanceEventType.CheckOut && openIn.HasValue)
            {
                totalMinutes += (int)Math.Round((e.EventTimeUtc - openIn.Value).TotalMinutes);
                openIn = null;
            }
        }

        var hasOpen = openIn.HasValue;

        day.FirstCheckInUtc = firstIn;
        day.LastCheckOutUtc = hasOpen ? null : lastOut;
        day.TotalWorkedMinutes = totalMinutes;
        day.IsIncomplete = hasOpen;

        var schedule = period.WorkSchedule;
        if (firstIn.HasValue && schedule is not null)
        {
            var localIn = _tz.LocalTime(firstIn.Value);
            var lateThreshold = schedule.StartTime.AddMinutes(schedule.GracePeriodMinutes);
            day.IsLate = localIn > lateThreshold;
        }

        if (!hasOpen && lastOut.HasValue && schedule is not null)
        {
            var localOut = _tz.LocalTime(lastOut.Value);
            day.IsEarlyLeave = localOut < schedule.EndTime;
        }

        day.Status = firstIn is null
            ? AttendanceStatus.NotStarted
            : day.IsLate ? AttendanceStatus.Late : AttendanceStatus.Present;
    }

    private AttendanceDaySummary ToSummary(AttendanceDay day) => new(
        day.WorkDate,
        day.FirstCheckInUtc.HasValue ? _tz.ToLocal(day.FirstCheckInUtc.Value) : null,
        day.LastCheckOutUtc.HasValue ? _tz.ToLocal(day.LastCheckOutUtc.Value) : null,
        day.TotalWorkedMinutes,
        day.Status,
        day.IsLate,
        day.IsEarlyLeave,
        day.IsIncomplete,
        HasOpenCheckIn(day));
}
