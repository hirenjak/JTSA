using JTSA.Utility;
using Xunit;

namespace JTSA.Tests;

public class VtsAutoConnectRetryTests
{
    [Fact]
    public void NextDelayDoublesUntilCap()
    {
        var first = VtsAutoConnectRetry.NextDelay(VtsAutoConnectRetry.FirstRetryDelay);
        Assert.Equal(TimeSpan.FromSeconds(10), first);

        var second = VtsAutoConnectRetry.NextDelay(first);
        Assert.Equal(TimeSpan.FromSeconds(20), second);

        var third = VtsAutoConnectRetry.NextDelay(second);
        Assert.Equal(VtsAutoConnectRetry.MaxRetryDelay, third);

        var capped = VtsAutoConnectRetry.NextDelay(VtsAutoConnectRetry.MaxRetryDelay);
        Assert.Equal(VtsAutoConnectRetry.MaxRetryDelay, capped);
    }
}
