using JTSA.Utility;
using Xunit;

namespace JTSA.Tests;

public class VtsTriggerMatcherTests
{
    [Fact]
    public void DisabledRuleDoesNotMatch()
    {
        var rule = Rule(VtsTriggerTypes.Chat, "hello", enabled: false);
        Assert.False(VtsTriggerMatcher.Matches(rule, StreamExpansionTriggerType.Chat, "hello world"));
    }

    [Fact]
    public void ChannelPointMatchesIdCaseInsensitive()
    {
        var rule = Rule(VtsTriggerTypes.ChannelPoint, "abc-1");
        Assert.True(VtsTriggerMatcher.Matches(rule, StreamExpansionTriggerType.ChannelPoint, "ABC-1"));
        Assert.False(VtsTriggerMatcher.Matches(rule, StreamExpansionTriggerType.ChannelPoint, "other"));
        Assert.False(VtsTriggerMatcher.Matches(rule, StreamExpansionTriggerType.Chat, "abc-1"));
    }

    [Fact]
    public void ChatMatchesSubstringIgnoreCase()
    {
        var rule = Rule(VtsTriggerTypes.Chat, "にゃん");
        Assert.True(VtsTriggerMatcher.Matches(rule, StreamExpansionTriggerType.Chat, "こんにちはにゃんこ"));
        Assert.False(VtsTriggerMatcher.Matches(rule, StreamExpansionTriggerType.Chat, "こんにちは"));
        Assert.False(VtsTriggerMatcher.Matches(rule, StreamExpansionTriggerType.Chat, ""));
    }

    [Fact]
    public void ScheduledTimeMatchesExactValue()
    {
        var rule = Rule(VtsTriggerTypes.ScheduledTime, "21:05");
        Assert.True(VtsTriggerMatcher.Matches(rule, StreamExpansionTriggerType.ScheduledTime, "21:05"));
        Assert.False(VtsTriggerMatcher.Matches(rule, StreamExpansionTriggerType.ScheduledTime, "21:00"));
    }

    [Fact]
    public void ObsStreamStartMatchesMainOrSub()
    {
        var main = Rule(VtsTriggerTypes.ObsStreamStart, "main");
        var sub = Rule(VtsTriggerTypes.ObsStreamStart, "sub");
        Assert.True(VtsTriggerMatcher.Matches(main, StreamExpansionTriggerType.ObsStreamStart, "main"));
        Assert.False(VtsTriggerMatcher.Matches(main, StreamExpansionTriggerType.ObsStreamStart, "sub"));
        Assert.True(VtsTriggerMatcher.Matches(sub, StreamExpansionTriggerType.ObsStreamStart, "SUB"));
    }

    [Fact]
    public void EventOnlyTriggersIgnoreValue()
    {
        var rule = Rule(VtsTriggerTypes.FirstChat, "");
        Assert.True(VtsTriggerMatcher.Matches(rule, StreamExpansionTriggerType.FirstChat, "someone"));
        Assert.False(VtsTriggerMatcher.Matches(rule, StreamExpansionTriggerType.Follow, "someone"));
    }

    private static VtsTriggerRule Rule(string type, string value, bool enabled = true) => new()
    {
        IsEnabled = enabled,
        TriggerType = type,
        TriggerValue = value
    };
}
