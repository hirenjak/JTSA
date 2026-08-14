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
    public void ReplaceLeavesContentUnchangedOutsideRaid()
    {
        const string content = "{raid_user}さん、ありがとう！";

        Assert.Equal(content, StreamExpansionPlaceholderReplacer.Replace(content, null));
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
    public void ReplaceLeavesChatPlaceholdersUnchangedOutsideChatTrigger()
    {
        const string content = "{chat_user} ({chat_login})";

        Assert.Equal(content, StreamExpansionPlaceholderReplacer.Replace(content, null));
    }
}
