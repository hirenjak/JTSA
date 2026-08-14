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
}
