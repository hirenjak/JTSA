using JTSA.Utility;
using Xunit;

namespace JTSA.Tests;

public class TitlePlaceholderReplacerTests
{
    [Theory]
    [InlineData("{date, yyyy年mm月dd日}", "2026年09月04日")]
    [InlineData("${date, yyyy年MM月dd日}", "2026年09月04日")]
    [InlineData("${date, yy/m/d}", "26/9/4")]
    [InlineData("{date, M}", "9")]
    [InlineData("{date, yyyy-MM-dd 'mm'}", "2026-09-04 mm")]
    [InlineData("${date} / {date, mm月dd日}", "2026/09/04 / 09月04日")]
    [InlineData("{date, }", "{date, }")]
    [InlineData("${date, yyyy'broken}", "${date, yyyy'broken}")]
    public void ReplaceDateSupportsCustomFormatsWithoutBreakingInvalidTemplates(string template, string expected)
    {
        Assert.Equal(expected, TitlePlaceholderReplacer.ReplaceDate(template, new DateTime(2026, 9, 4, 12, 35, 0)));
    }

    [Fact]
    public void ReplaceTitle_ReplacesEveryTitlePlaceholder()
    {
        var result = TitlePlaceholderReplacer.ReplaceTitle(
            "配信タイトル",
            "${1} ${title} ${1} ${date}");

        Assert.Equal("${1} 配信タイトル ${1} ${date}", result);
    }

    [Fact]
    public void ReplaceTitle_ReturnsTitleWhenTemplateIsEmpty()
    {
        var result = TitlePlaceholderReplacer.ReplaceTitle("配信タイトル", "");

        Assert.Equal("配信タイトル", result);
    }

    [Fact]
    public void ReplaceTitle_ReplacesJapaneseCategoryPlaceholder()
    {
        var result = TitlePlaceholderReplacer.ReplaceTitle(
            "配信タイトル",
            "${title}【${category_ja}】",
            "日本語カテゴリ");

        Assert.Equal("配信タイトル【日本語カテゴリ】", result);
    }

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
