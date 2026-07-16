using Staj360.Application.Abstractions;

namespace Staj360.UnitTests.TestSupport;

public sealed class TestClock : IClock
{
    public TestClock(DateTime utcNow) => UtcNow = DateTime.SpecifyKind(utcNow, DateTimeKind.Utc);

    public DateTime UtcNow { get; set; }
}
