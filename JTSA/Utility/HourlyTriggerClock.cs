namespace JTSA.Utility;

/// <summary>Local wall-clock hours; missed hours are not replayed after sleep or a clock change.</summary>
internal sealed class HourlyTriggerClock(DateTime startedAt)
{
    private DateTime lastHour = TruncateHour(startedAt);

    public bool TryTick(DateTime now)
    {
        var hour = TruncateHour(now);
        // A high-water mark also prevents duplicate execution when the clock moves back.
        if (hour <= lastHour) return false;
        lastHour = hour;
        return now.Minute == 0;
    }

    private static DateTime TruncateHour(DateTime value) =>
        new(value.Year, value.Month, value.Day, value.Hour, 0, 0);
}
