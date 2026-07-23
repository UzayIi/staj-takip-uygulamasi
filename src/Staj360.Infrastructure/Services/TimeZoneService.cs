using Microsoft.Extensions.Options;
using Staj360.Application.Abstractions;
using Staj360.Infrastructure.Configuration;

namespace Staj360.Infrastructure.Services;

/// <summary>
/// UTC ile kurum yerel saati arasında dönüşüm. Zaman dilimi ayardan (Organization:TimeZone)
/// okunur; bulunamazsa Europe/Istanbul'a düşer.
/// </summary>
public class TimeZoneService : ITimeZoneService
{
    private readonly TimeZoneInfo _timeZone;

    public TimeZoneService(IOptions<OrganizationOptions> options)
    {
        _timeZone = ResolveTimeZone(options.Value.TimeZone);
    }

    public DateTime ToLocal(DateTime utc)
    {
        var utcKind = DateTime.SpecifyKind(utc, DateTimeKind.Utc);
        return TimeZoneInfo.ConvertTimeFromUtc(utcKind, _timeZone);
    }

    public DateTime ToUtc(DateTime local)
    {
        var unspecified = DateTime.SpecifyKind(local, DateTimeKind.Unspecified);
        return TimeZoneInfo.ConvertTimeToUtc(unspecified, _timeZone);
    }

    public DateOnly LocalDate(DateTime utc) => DateOnly.FromDateTime(ToLocal(utc));

    public TimeOnly LocalTime(DateTime utc) => TimeOnly.FromDateTime(ToLocal(utc));

    private static TimeZoneInfo ResolveTimeZone(string id)
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(id);
        }
        catch (TimeZoneNotFoundException)
        {
            // Windows kimliği ile de dene (Turkey Standard Time).
            try { return TimeZoneInfo.FindSystemTimeZoneById("Turkey Standard Time"); }
            catch { return TimeZoneInfo.Utc; }
        }
    }
}
