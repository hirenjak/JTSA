using JTSA.Utility;
using Xunit;

namespace JTSA.Tests;

public class TitlePlaceholderReplacerTests
{
    [Fact]
    public void ReplaceDateReplacesAllDatePlaceholders()
    {
        var dateTime = new DateTime(2026, 8, 14, 23, 59, 0);

        var result = TitlePlaceholderReplacer.ReplaceDate(
            "${date} 配信開始！ 次回は${date}",
            dateTime);

        Assert.Equal("2026/08/14 配信開始！ 次回は2026/08/14", result);
    }

    [Fact]
    public void ReplaceDateLeavesTitleWithoutPlaceholderUnchanged()
    {
        const string title = "通常の配信タイトル";

        var result = TitlePlaceholderReplacer.ReplaceDate(title, new DateTime(2026, 8, 14));

        Assert.Equal(title, result);
    }
}
