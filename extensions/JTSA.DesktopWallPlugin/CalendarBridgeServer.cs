using System.IO;
using System.IO.Pipes;
using System.Windows.Threading;
using JTSA.Plugin.Abstractions;
using JtsaCalendarBridge;

namespace JTSA.DesktopWallPlugin;

internal sealed class CalendarBridgeServer : IDisposable
{
    readonly CancellationTokenSource lifetime = new();
    readonly NamedPipeServerStream pipe;
    readonly Task serving;

    internal CalendarBridgeServer(IJtsaCalendarPluginContext calendar, Dispatcher dispatcher, Action<Exception> onError, string? pipeName = null)
    {
        // Keep one instance alive across requests, so a second JTSA cannot silently take over.
        pipe = new NamedPipeServerStream(pipeName ?? CalendarBridgeProtocol.PipeName, PipeDirection.InOut, 1,
            PipeTransmissionMode.Byte, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
        serving = ServeAsync(calendar, dispatcher, onError);
    }

    async Task ServeAsync(IJtsaCalendarPluginContext calendar, Dispatcher dispatcher, Action<Exception> onError)
    {
        try
        {
            while (!lifetime.IsCancellationRequested)
            {
                await pipe.WaitForConnectionAsync(lifetime.Token).ConfigureAwait(false);
                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token);
                timeout.CancelAfter(TimeSpan.FromSeconds(5));
                try
                {
                    var request = await CalendarBridgeProtocol.ReadAsync<CalendarRequest>(pipe, timeout.Token).ConfigureAwait(false);
                    CalendarResponse response;
                    try
                    {
                        var (from, to) = CalendarBridgeProtocol.GetRange(request);
                        var entries = await dispatcher.InvokeAsync(() => calendar.GetCalendarEntries(from, to),
                            DispatcherPriority.Background, timeout.Token).Task.ConfigureAwait(false);
                        response = new(CalendarBridgeProtocol.Version, entries
                            .Where(e => e.Date.Date >= from && e.Date.Date < to && e.StartTime >= TimeSpan.Zero && e.StartTime < TimeSpan.FromDays(1))
                            .OrderBy(e => e.Date).ThenBy(e => e.StartTime).ThenBy(e => e.Id)
                            .Select(e => new SharedCalendarEntry(e.Id, DateTime.SpecifyKind(e.Date.Date + e.StartTime, DateTimeKind.Unspecified),
                                string.IsNullOrWhiteSpace(e.ResolvedTitle) ? e.Content : e.ResolvedTitle, e.CategoryName))
                            .Where(e => request.UpcomingFrom == null || e.Start >= request.UpcomingFrom.Value)
                            .Take(request.UpcomingFrom.HasValue ? 3 : int.MaxValue).ToArray(), IsUpcoming: request.UpcomingFrom.HasValue);
                    }
                    catch (OperationCanceledException) { throw; }
                    catch (Exception ex)
                    {
                        onError(ex);
                        response = new(CalendarBridgeProtocol.Version, [], "JTSAの予定を取得できませんでした。");
                    }
                    await CalendarBridgeProtocol.WriteAsync(pipe, response, timeout.Token).ConfigureAwait(false);
                }
                catch (Exception ex) when (ex is IOException or OperationCanceledException or System.Text.Json.JsonException)
                {
                    // A closed/timed-out client must not prevent the next panel from connecting.
                }
                finally
                {
                    if (!lifetime.IsCancellationRequested && pipe.IsConnected) pipe.Disconnect();
                }
            }
        }
        catch (Exception ex)
        {
            if (!lifetime.IsCancellationRequested) onError(ex);
        }
    }

    internal bool IsRunning => !serving.IsCompleted && !lifetime.IsCancellationRequested;

    public void Dispose()
    {
        lifetime.Cancel();
        pipe.Dispose();
        // Never synchronously wait on a request that may be awaiting the UI dispatcher.
        _ = serving.ContinueWith(_ => lifetime.Dispose(), TaskScheduler.Default);
    }
}
