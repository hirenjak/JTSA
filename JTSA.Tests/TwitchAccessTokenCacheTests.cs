using JTSA.Utility;
using Xunit;

namespace JTSA.Tests;

public class TwitchAccessTokenCacheTests
{
    [Fact]
    public void TokensAreIsolatedAndReauthenticationInvalidatesCache()
    {
        var cache = new TwitchAccessTokenCache();
        var now = DateTimeOffset.UtcNow;
        cache.Store(1, "refresh-main", "main", 3600, now);
        cache.Store(2, "refresh-sub", "sub", 1800, now);
        Assert.True(cache.TryGet(1, "refresh-main", now, out var main));
        Assert.Equal("main", main);
        Assert.True(cache.TryGet(2, "refresh-sub", now, out var sub));
        Assert.Equal("sub", sub);
        Assert.False(cache.TryGet(2, "reauthenticated", now, out _));
        Assert.False(cache.TryGet(3, "refresh-main", now, out _));
        Assert.False(cache.TryGet(2, "refresh-sub", now.AddMinutes(25), out _));
        Assert.True(cache.TryGet(1, "refresh-main", now.AddMinutes(25), out _));
    }

    [Theory]
    [InlineData(3600, 3300)]
    [InlineData(120, 60)]
    public void RefreshBeforeExpiry(int lifetime, int refreshAfter)
    {
        var cache = new TwitchAccessTokenCache();
        var now = DateTimeOffset.UtcNow;
        cache.Store(1, "refresh", "token", lifetime, now);
        Assert.True(cache.TryGet(1, "refresh", now.AddSeconds(refreshAfter - 1), out _));
        Assert.False(cache.TryGet(1, "refresh", now.AddSeconds(refreshAfter), out _));
        Assert.False(cache.TryGet(1, "refresh", now.AddSeconds(lifetime), out _));
    }
}
