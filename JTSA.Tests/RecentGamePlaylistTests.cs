using JTSA.Utility;
using Xunit;

namespace JTSA.Tests;

public class RecentGamePlaylistTests
{
    [Fact]
    public void CurrentComesFirstWithFiveDistinctPastGames()
    {
        Assert.Equal(new[] { "current", "a", "b", "c", "d", "e" },
            RecentGamePlaylist.SelectCategoryIds("current", ["a", "current", "a", "", "b", "c", "d", "e", "f"]));
    }

    [Fact]
    public void MissingCurrentOrHistoryDoesNotCreateBlankCards()
    {
        Assert.Equal(new[] { "a" }, RecentGamePlaylist.SelectCategoryIds("", ["", "a", "a"]));
        Assert.Equal(new[] { "current" }, RecentGamePlaylist.SelectCategoryIds("current", []));
        Assert.Empty(RecentGamePlaylist.SelectCategoryIds("", []));
    }
}
