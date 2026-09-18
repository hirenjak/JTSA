namespace JTSA.Utility;

internal static class VtsAutoConnectRetry
{
    public static readonly TimeSpan InitialDelay = TimeSpan.FromSeconds(2);
    public static readonly TimeSpan FirstRetryDelay = TimeSpan.FromSeconds(5);
    public static readonly TimeSpan MaxRetryDelay = TimeSpan.FromSeconds(30);

    public static TimeSpan NextDelay(TimeSpan current)
    {
        var seconds = current <= TimeSpan.Zero
            ? FirstRetryDelay.TotalSeconds
            : current.TotalSeconds * 2;
        return TimeSpan.FromSeconds(Math.Min(seconds, MaxRetryDelay.TotalSeconds));
    }
}
