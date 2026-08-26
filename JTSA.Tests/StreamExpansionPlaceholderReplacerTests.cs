using JTSA.Utility;
using Xunit;

namespace JTSA.Tests;

public class StreamExpansionPlaceholderReplacerTests
{
    [Fact]
    public void ReplaceExpandsRaidPlaceholders()
    {
        var result = StreamExpansionPlaceholderReplacer.Replace(
            "{raid_user}さん、{raid_title} / {raid_category} からありがとう！",
            new RaidPlaceholderValues("Raider", "Last stream", "Just Chatting"));

        Assert.Equal("Raiderさん、Last stream / Just Chatting からありがとう！", result);
    }

    [Fact]
    public void ReplaceClearsUnavailableRaidValues()
    {
        const string content = "{raid_user}さん、ありがとう！";

        Assert.Equal("さん、ありがとう！", StreamExpansionPlaceholderReplacer.Replace(content, null));
    }

    [Fact]
    public void ReplaceSupportsCaseInsensitivePlaceholders()
    {
        var result = StreamExpansionPlaceholderReplacer.Replace(
            "{RAID_USER}: {Raid_Title} [{RAID_CATEGORY}]",
            new RaidPlaceholderValues("Raider", "Title", "Category"));

        Assert.Equal("Raider: Title [Category]", result);
    }

    [Fact]
    public void ReplaceExpandsChatUserPlaceholders()
    {
        var result = StreamExpansionPlaceholderReplacer.Replace(
            "{chat_user} ({CHAT_LOGIN})",
            null,
            new ChatPlaceholderValues("表示名", "login_name"));

        Assert.Equal("表示名 (login_name)", result);
    }

    [Fact]
    public void ReplaceClearsUnavailableChatValues()
    {
        const string content = "{chat_user} ({chat_login})";

        Assert.Equal(" ()", StreamExpansionPlaceholderReplacer.Replace(content, null));
    }

    [Fact]
    public void ReplaceExpandsTriggerAndStreamPlaceholders()
    {
        var result = StreamExpansionPlaceholderReplacer.Replace(
            "{trigger_type}|{trigger_value}|{trigger_obs}|{stream_title}|{stream_category}|{unknown}",
            null,
            null,
            new StreamExpansionTriggerValues("obs_stream_start", "main", "main", "Title", "Category"));

        Assert.Equal("obs_stream_start|main|main|Title|Category|{unknown}", result);
    }

    [Fact]
    public void ReplaceExpandsJapaneseStreamCategoryPlaceholder()
    {
        var result = StreamExpansionPlaceholderReplacer.Replace(
            "カテゴリ: {stream_category_ja}",
            null,
            trigger: new StreamExpansionTriggerValues(
                "chat", "message", "", "Title", "Category", "", "日本語カテゴリ"));

        Assert.Equal("カテゴリ: 日本語カテゴリ", result);
    }

    [Fact]
    public void ReplaceExpandsStreamSupportUserPlaceholders()
    {
        var result = StreamExpansionPlaceholderReplacer.Replace(
            "Bits\n{stream_bits_users}\nSubs\n{stream_subscribe_users}\nRaids\n{stream_raid_users}\nFollows\n{stream_follow_users}",
            null,
            trigger: new StreamExpansionTriggerValues(
                "chat", "message", "", "", "", "", "",
                "alice: 150 Bits", "bob: 2件", "carol: 20人", "dave"));

        Assert.Equal(
            "Bits\nalice: 150 Bits\nSubs\nbob: 2件\nRaids\ncarol: 20人\nFollows\ndave",
            result);
    }

    [Fact]
    public void ReplaceExpandsChannelPointInput()
    {
        var result = StreamExpansionPlaceholderReplacer.Replace(
            "入力: {channel_point_input}",
            null,
            null,
            new StreamExpansionTriggerValues("channel_point", "reward-id", "", "", "", "リクエスト文言"));

        Assert.Equal("入力: リクエスト文言", result);
    }

    [Theory]
    [InlineData("!request テスト曲", "!request", "テスト曲")]
    [InlineData("!REQUEST    テスト曲  ", "!request", "テスト曲")]
    [InlineData("先頭 !request テスト曲", "!request", "テスト曲")]
    [InlineData("!request", "!request", "")]
    public void ResolveChannelPointInputUsesTextAfterChatTrigger(
        string message, string triggerComment, string expected)
    {
        var rule = new JTSA.Models.T_StreamExpansionHeader
        {
            TriggerComment = triggerComment,
            UpdatedDateTime = DateTime.Now
        };

        var result = StreamExpansionService.ResolveChannelPointInput(
            rule, StreamExpansionTriggerType.Chat, message, "元の入力");

        Assert.Equal(expected, result);
    }

    [Fact]
    public void ResolveChannelPointInputKeepsRewardInputForChannelPointTrigger()
    {
        var rule = new JTSA.Models.T_StreamExpansionHeader
        {
            TriggerComment = "!request",
            UpdatedDateTime = DateTime.Now
        };

        var result = StreamExpansionService.ResolveChannelPointInput(
            rule, StreamExpansionTriggerType.ChannelPoint, "reward-id", "リクエスト文言");

        Assert.Equal("リクエスト文言", result);
    }
}
