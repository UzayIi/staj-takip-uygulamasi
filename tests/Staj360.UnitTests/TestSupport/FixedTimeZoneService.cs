using Staj360.Application.Abstractions;

namespace Staj360.UnitTests.TestSupport;

/// <summary>Testlerde UTC+3 (Istanbul) sabit ofsetiyle çalışır; sistem TZ kimliğine bağımlı değildir.</summary>
public sealed class FixedTimeZoneService : ITimeZoneService
{
    private static readonly TimeSpan Offset = TimeSpan.FromHours(3);

    public DateTime ToLocal(DateTime utc) => DateTime.SpecifyKind(utc, DateTimeKind.Utc).Add(Offset);

    public DateTime ToUtc(DateTime local) => DateTime.SpecifyKind(local.Subtract(Offset), DateTimeKind.Utc);

    public DateOnly LocalDate(DateTime utc) => DateOnly.FromDateTime(ToLocal(utc));

    public TimeOnly LocalTime(DateTime utc) => TimeOnly.FromDateTime(ToLocal(utc));
}
