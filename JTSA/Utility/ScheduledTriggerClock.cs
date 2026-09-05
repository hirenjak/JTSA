namespace JTSA.Utility;

/// <summary>Checks each local five-minute slot once, without replaying missed slots.</summary>
internal sealed class ScheduledTriggerClock(DateTime startedAt)
{
    private DateTime lastMinute = TruncateMinute(startedAt);

    public bool TryTick(DateTime now)
    {
        var minute = TruncateMinute(now);
        if (minute <= lastMinute) return false;
        lastMinute = minute;
        return now.Minute % 5 == 0;
    }

    private static DateTime TruncateMinute(DateTime value) =>
        new(value.Year, value.Month, value.Day, value.Hour, value.Minute, 0);
}
