using JTSA.Utility;
using Xunit;

namespace JTSA.Tests;

public sealed class SteamHelperTests
{
    [Theory]
    [InlineData("https://store.steampowered.com/app/1245620/ELDEN_RING/", "1245620")]
    [InlineData("http://store.steampowered.com/app/730", "730")]
    [InlineData("store.steampowered.com/app/10?l=japanese", "10")]
    public void GetSteamAppId_ValidStoreUrl_ReturnsAppId(string url, string expected)
    {
        Assert.Equal(expected, SteamHelper.GetSteamAppId(url));
    }

    [Theory]
    [InlineData("")]
    [InlineData("https://example.com/app/123")]
    [InlineData("https://store.steampowered.com/sub/123")]
    [InlineData("https://store.steampowered.com/app/not-a-number")]
    public void GetSteamAppId_InvalidUrl_ReturnsNull(string url)
    {
        Assert.Null(SteamHelper.GetSteamAppId(url));
    }

    [Fact]
    public async Task GetSteamHeaderImageUrlAsync_NullAppId_ReturnsNullWithoutNetworkRequest()
    {
        Assert.Null(await SteamHelper.GetSteamHeaderImageUrlAsync(null!));
    }
}
