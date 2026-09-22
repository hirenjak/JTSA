using JTSA.Utility;
using Xunit;

namespace JTSA.Tests;

public class SpeechMuteFilterTests
{
    [Fact]
    public void ParseIgnoresBlankLines()
    {
        var muted = SpeechMuteFilter.Parse(" \n\nfoo\n  BAR  \n");

        Assert.Equal(2, muted.Count);
        Assert.True(SpeechMuteFilter.IsMuted(muted, "foo"));
        Assert.True(SpeechMuteFilter.IsMuted(muted, "bar"));
    }

    [Fact]
    public void EmptyOrWhitespaceIsNotMuted()
    {
        var muted = SpeechMuteFilter.Parse(null);

        Assert.False(SpeechMuteFilter.IsMuted(muted, "anyone"));
        Assert.False(SpeechMuteFilter.IsMuted(muted, " "));
        Assert.False(SpeechMuteFilter.IsMuted(muted, null));
    }

    [Fact]
    public void IsMutedIgnoresCase()
    {
        var muted = SpeechMuteFilter.Parse("NightBot");

        Assert.True(SpeechMuteFilter.IsMuted(muted, "nightbot"));
        Assert.False(SpeechMuteFilter.IsMuted(muted, "streamelements"));
    }

    [Fact]
    public void ToggleAddsAndRemoves()
    {
        var muted = SpeechMuteFilter.Toggle([], "NightBot");
        Assert.True(SpeechMuteFilter.IsMuted(muted, "nightbot"));

        muted = SpeechMuteFilter.Toggle(muted, "nightbot");
        Assert.False(SpeechMuteFilter.IsMuted(muted, "NightBot"));
        Assert.Equal("", SpeechMuteFilter.Serialize(muted));
    }
}
