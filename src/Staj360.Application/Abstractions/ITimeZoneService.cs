namespace Staj360.Application.Abstractions;

/// <summary>
/// UTC ile kurum yerel saati (Europe/Istanbul) arasında dönüşüm sağlar.
/// İş günü belirlemede yerel tarih kullanılır.
/// </summary>
public interface ITimeZoneService
{
    DateTime ToLocal(DateTime utc);
    DateTime ToUtc(DateTime local);
    DateOnly LocalDate(DateTime utc);
    TimeOnly LocalTime(DateTime utc);
}
