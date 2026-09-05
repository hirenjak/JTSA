using JTSA.Models;
using JTSA.Utility;
using TwitchLib.Api;
using Xunit;

namespace JTSA.Tests;

public class AdTriggerTests
{
    [Fact]
    public async Task DuplicateAndOutOfOrderBeginsAreIgnored()
    {
        await using var monitor = new AdTriggerMonitor(new TwitchAPI(), "test", _ => { });
        var events = new List<string>();
        monitor.Triggered += (_, value) => events.Add(value);
        var now = DateTimeOffset.UtcNow;
        monitor.OnBegin(now, 60);
        monitor.OnBegin(now, 60);
        monitor.OnBegin(now.AddMinutes(-1), 30);
        monitor.OnBegin(now.AddMinutes(10), 90);
        Assert.Equal(new[] { "60", "90" }, events);
    }

    [Theory]
    [InlineData(61, false)]
    [InlineData(60, true)]
    [InlineData(45, true)]
    [InlineData(30, false)]
    [InlineData(0, false)]
    [InlineData(-1, false)]
    public void UpcomingHasBoundedWindowAndNeverFiresAfterStart(int secondsRemaining, bool expected)
    {
        var now = DateTimeOffset.UtcNow;
        Assert.Equal(expected, AdTriggerMonitor.IsUpcoming(now, now.AddSeconds(secondsRemaining), 1));
    }

    [Fact]
    public async Task EstimatedEndFiresOnceAndSkipsOldEndAfterSleep()
    {
        await using var monitor = new AdTriggerMonitor(new TwitchAPI(), "test", _ => { });
        var ends = new List<string>();
        monitor.Triggered += (type, value) => { if (type == StreamExpansionTriggerType.AdEnd) ends.Add(value); };
        var now = DateTimeOffset.UtcNow;
        monitor.OnBegin(now, 60);
        monitor.CheckEnd(now.AddSeconds(59));
        Assert.Empty(ends);
        monitor.CheckEnd(now.AddSeconds(60));
        monitor.CheckEnd(now.AddSeconds(61));
        Assert.Equal(new[] { "60" }, ends);
        monitor.OnBegin(now.AddMinutes(10), 60);
        monitor.CheckEnd(now.AddHours(1));
        Assert.Single(ends);
    }

    [Fact]
    public void AdTriggersMatchIndependentlyAndRespectActiveFlag()
    {
        var rule = new T_StreamExpansionHeader { Name = "ads", IsActive = true, IsAdUpcoming = true, AdAdvanceMinutes = 3, UpdatedDateTime = DateTime.Now };
        Assert.True(StreamExpansionService.Matches(rule, StreamExpansionTriggerType.AdUpcoming, "3"));
        Assert.False(StreamExpansionService.Matches(rule, StreamExpansionTriggerType.AdUpcoming, "1"));
        Assert.False(StreamExpansionService.Matches(rule, StreamExpansionTriggerType.AdStart, "60"));
        rule.IsAdStart = true;
        rule.IsAdEnd = true;
        Assert.True(StreamExpansionService.Matches(rule, StreamExpansionTriggerType.AdStart, "60"));
        Assert.True(StreamExpansionService.Matches(rule, StreamExpansionTriggerType.AdEnd, "60"));
        rule.IsActive = false;
        Assert.False(StreamExpansionService.Matches(rule, StreamExpansionTriggerType.AdEnd, "60"));
    }
}
