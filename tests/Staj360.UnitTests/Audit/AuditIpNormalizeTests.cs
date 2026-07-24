using Staj360.Infrastructure.Services;

namespace Staj360.UnitTests.Audit;

public class AuditIpNormalizeTests
{
    [Theory]
    [InlineData("127.0.0.1", "127.0.0.1")]
    [InlineData("::1", "::1")]
    [InlineData("::ffff:192.168.1.10", "192.168.1.10")]
    public void NormalizeIp_MapsIpv4Mapped(string input, string expected)
    {
        Assert.Equal(expected, AuditLogService.NormalizeIp(input));
    }

    [Fact]
    public void NormalizeIp_NullSafe()
    {
        Assert.Null(AuditLogService.NormalizeIp(null));
        Assert.Null(AuditLogService.NormalizeIp(" "));
    }
}
