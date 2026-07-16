using Staj360.Application.Abstractions;

namespace Staj360.Infrastructure.Services;

public class SystemClock : IClock
{
    public DateTime UtcNow => DateTime.UtcNow;
}
