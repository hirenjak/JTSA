using JTSA.Models;
using JTSA.Utility;
using Xunit;

namespace JTSA.Tests;

public class StreamExpansionScheduledTimeTests
{
    [Theory]
    [InlineData(0, 0, "00:00", true)]
    [InlineData(23, 55, "23:55", true)]
    [InlineData(12, 5, "12:05", true)]
    [InlineData(12, 5, "13:05", false)]
    [InlineData(12, 5, "12:10", false)]
    [InlineData(24, 0, "24:00", false)]
    [InlineData(12, 3, "12:03", false)]
    public void MatchesOnlyValidConfiguredTime(int hour, int minute, string value, bool expected)
    {
        var rule = new T_StreamExpansionHeader
        {
            Name = "scheduled", IsActive = true, IsScheduledTime = true,
            ScheduledHour = hour, ScheduledMinute = minute, UpdatedDateTime = DateTime.Now
        };
        Assert.Equal(expected, StreamExpansionService.Matches(rule, StreamExpansionTriggerType.ScheduledTime, value));
        Assert.False(StreamExpansionService.Matches(rule, StreamExpansionTriggerType.Hourly, value));
        rule.IsScheduledTime = false;
        Assert.False(StreamExpansionService.Matches(rule, StreamExpansionTriggerType.ScheduledTime, value));
        rule.IsScheduledTime = true;
        rule.IsActive = false;
        Assert.False(StreamExpansionService.Matches(rule, StreamExpansionTriggerType.ScheduledTime, value));
    }

    [Fact]
    public void ClockSkipsDuplicatesAndMissedSlotsAndRunsNextDay()
    {
        var start = new DateTime(2026, 9, 4, 23, 54, 59);
        var clock = new ScheduledTriggerClock(start);
        Assert.False(clock.TryTick(start));
        Assert.True(clock.TryTick(start.AddSeconds(1)));
        Assert.False(clock.TryTick(start.AddSeconds(2)));
        Assert.False(clock.TryTick(start.AddMinutes(8)));
        Assert.False(clock.TryTick(start.AddSeconds(1)));
        Assert.True(clock.TryTick(start.AddDays(1).AddSeconds(1)));
    }
}
