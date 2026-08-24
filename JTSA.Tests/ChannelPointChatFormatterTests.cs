using JTSA.Utility;
using Xunit;

namespace JTSA.Tests;

public class ChannelPointChatFormatterTests
{
    [Fact]
    public void FormatCombinesRewardAndUserInputInOneMessage()
    {
        var result = ChannelPointChatFormatter.Format("セリフリクエスト", "おはよう！");

        Assert.Equal($"🎁 セリフリクエスト{Environment.NewLine}おはよう！", result);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void FormatDoesNotAddBlankLineWithoutUserInput(string userInput)
    {
        var result = ChannelPointChatFormatter.Format("水を飲む", userInput);

        Assert.Equal("🎁 水を飲む", result);
    }

    [Fact]
    public void CreatePartsUsesBrighterGreenForUserInput()
    {
        var parts = ChannelPointChatFormatter.CreateParts("セリフリクエスト", "おはよう！");

        Assert.Equal(2, parts.Count);
        Assert.Equal(ChannelPointChatFormatter.RewardColor, parts[0].Foreground);
        Assert.Equal(ChannelPointChatFormatter.UserInputColor, parts[1].Foreground);
        Assert.StartsWith(Environment.NewLine, parts[1].Text);
    }
}
