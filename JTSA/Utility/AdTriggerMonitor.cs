using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using TwitchLib.Api;

namespace JTSA.Utility;

internal sealed class AdTriggerMonitor(TwitchAPI api, string broadcasterId, Action<string> log) : IAsyncDisposable
{
    private readonly CancellationTokenSource stop = new();
    private readonly HttpClient http = new() { Timeout = TimeSpan.FromSeconds(10) };
    private readonly object sync = new();
    private DateTimeOffset lastStart;
    private DateTimeOffset? endAt;
    private int duration;
    private Task? loop;
    private readonly HashSet<(long, int)> notified = [];
    internal event Action<StreamExpansionTriggerType, string>? Triggered;

    public void Start() => loop ??= RunAsync();

    public void OnBegin(DateTimeOffset startedAt, int seconds)
    {
        lock (sync)
        {
            if (stop.IsCancellationRequested || seconds <= 0 || startedAt <= lastStart) return;
            lastStart = startedAt;
            duration = seconds;
            endAt = startedAt.AddSeconds(seconds);
        }
        Raise(StreamExpansionTriggerType.AdStart, seconds);
    }

    private void Raise(StreamExpansionTriggerType type, int value)
    {
        if (stop.IsCancellationRequested) return;
        try { Triggered?.Invoke(type, value.ToString(System.Globalization.CultureInfo.InvariantCulture)); }
        catch (Exception ex) { log($"CMトリガー実行失敗：{ex.GetType().Name}"); }
    }

    internal static bool IsUpcoming(DateTimeOffset now, DateTimeOffset next, int minutes) =>
        next > now && now >= next.AddMinutes(-minutes) && now < next.AddMinutes(-minutes).AddSeconds(30);

    internal void CheckEnd(DateTimeOffset now)
    {
        int? ended = null;
        lock (sync)
        {
            if (endAt is { } end && now >= end)
            {
                // Do not replay an old estimated end after sleep.
                if (now - end < TimeSpan.FromSeconds(30)) ended = duration;
                endAt = null;
            }
        }
        if (ended is { } seconds) Raise(StreamExpansionTriggerType.AdEnd, seconds);
    }

    private async Task RunAsync()
    {
        var nextPoll = DateTimeOffset.MinValue;
        var lastError = DateTimeOffset.MinValue;
        try
        {
            while (!stop.IsCancellationRequested)
            {
                var now = DateTimeOffset.UtcNow;
                CheckEnd(now);
                if (now >= nextPoll)
                {
                    nextPoll = now.AddSeconds(15);
                    try
                    {
                        using var request = new HttpRequestMessage(HttpMethod.Get,
                            $"https://api.twitch.tv/helix/channels/ads?broadcaster_id={Uri.EscapeDataString(broadcasterId)}");
                        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", api.Settings.AccessToken);
                        request.Headers.Add("Client-Id", api.Settings.ClientId);
                        using var response = await http.SendAsync(request, stop.Token);
                        response.EnsureSuccessStatusCode();
                        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(stop.Token));
                        var data = json.RootElement.GetProperty("data");
                        if (data.GetArrayLength() > 0 && DateTimeOffset.TryParse(
                            data[0].GetProperty("next_ad_at").ToString(), System.Globalization.CultureInfo.InvariantCulture,
                            System.Globalization.DateTimeStyles.AssumeUniversal, out var next))
                        {
                            var timestamp = next.ToUnixTimeSeconds();
                            now = DateTimeOffset.UtcNow;
                            notified.RemoveWhere(x => x.Item1 < now.AddHours(-1).ToUnixTimeSeconds());
                            for (var minutes = 1; minutes <= 10; minutes++)
                                if (IsUpcoming(now, next, minutes) && notified.Add((timestamp, minutes)))
                                    Raise(StreamExpansionTriggerType.AdUpcoming, minutes);
                        }
                    }
                    catch (OperationCanceledException) when (stop.IsCancellationRequested) { break; }
                    catch (Exception ex)
                    {
                        // Failed requests never use an old schedule; throttle errors and retries.
                        nextPoll = DateTimeOffset.UtcNow.AddMinutes(1);
                        if (now - lastError > TimeSpan.FromMinutes(5))
                        {
                            lastError = now;
                            log($"CM予定取得失敗：{ex.GetType().Name}。Twitchを再認証し channel:read:ads を許可してください。");
                        }
                    }
                }
                await Task.Delay(1000, stop.Token);
            }
        }
        catch (OperationCanceledException) when (stop.IsCancellationRequested) { }
    }

    public async ValueTask DisposeAsync()
    {
        await stop.CancelAsync();
        if (loop is not null) await loop;
        http.Dispose();
        stop.Dispose();
    }
}
