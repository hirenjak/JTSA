using JTSA.Models;
using JTSA.Utility;
using Xunit;

namespace JTSA.Tests;

public class StreamExpansionHourlyTests
{
    [Fact]
    public void FiresOnceAtNextHourIncludingMidnight()
    {
        var start = new DateTime(2026, 9, 4, 23, 59, 58);
        var clock = new HourlyTriggerClock(start);
        Assert.False(clock.TryTick(start.AddSeconds(1)));
        Assert.True(clock.TryTick(start.AddSeconds(2)));
        Assert.False(clock.TryTick(start.AddSeconds(3)));
        Assert.True(clock.TryTick(start.AddHours(1).AddSeconds(2)));
    }

    [Fact]
    public void DoesNotReplayMissedHoursOrRepeatAfterClockMovesBack()
    {
        var start = new DateTime(2026, 9, 4, 10, 0, 0);
        var clock = new HourlyTriggerClock(start);
        Assert.False(clock.TryTick(start));
        Assert.False(clock.TryTick(start.AddHours(3).AddMinutes(15)));
        Assert.False(clock.TryTick(start.AddHours(3)));
        Assert.True(clock.TryTick(start.AddHours(4)));
        Assert.False(clock.TryTick(start.AddHours(3)));
        Assert.False(clock.TryTick(start.AddHours(4)));
    }

    [Theory]
    [InlineData(true, true, true)]
    [InlineData(true, false, false)]
    [InlineData(false, true, false)]
    public void OnlyEnabledHourlyRulesMatch(bool active, bool hourly, bool expected)
    {
        var rule = new T_StreamExpansionHeader
        {
            Name = "hourly", IsActive = active, IsHourly = hourly, UpdatedDateTime = DateTime.Now
        };
        Assert.Equal(expected, StreamExpansionService.Matches(rule, StreamExpansionTriggerType.Hourly, "12:00"));
        Assert.False(StreamExpansionService.Matches(rule, StreamExpansionTriggerType.Follow, ""));
    }
}
