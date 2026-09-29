using System.IO.Pipes;
using System.Windows.Threading;
using JTSA.DesktopWallPlugin;
using JTSA.Plugin.Abstractions;
using JtsaCalendarBridge;
using Xunit;

namespace JTSA.Tests;

public class CalendarBridgeServerTests
{
    [Fact]
    public async Task ServerHandlesInvalidRequestThenFreshSnapshotAndEmptyMonth()
    {
        var ready = new TaskCompletionSource<Dispatcher>(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() => { ready.SetResult(Dispatcher.CurrentDispatcher); Dispatcher.Run(); }) { IsBackground = true };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        var dispatcher = await ready.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var calendar = new Calendar(dispatcher);
        var name = "JTSA.Calendar.Test." + Guid.NewGuid().ToString("N");
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        try
        {
            using var server = new CalendarBridgeServer(calendar, dispatcher, _ => { }, name);
            async Task<CalendarResponse> Query(CalendarRequest request)
            {
                using var client = new NamedPipeClientStream(".", name, PipeDirection.InOut, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
                await client.ConnectAsync(timeout.Token);
                await CalendarBridgeProtocol.WriteAsync(client, request, timeout.Token);
                return await CalendarBridgeProtocol.ReadAsync<CalendarResponse>(client, timeout.Token);
            }
            Assert.NotNull((await Query(new(99, 2026, 9))).Error);
            var response = await Query(new(1, 2026, 9));
            Assert.Null(response.Error);
            Assert.Equal("確定タイトル", Assert.Single(response.Entries).Title);
            Assert.Equal(new DateTime(2026, 9, 1), calendar.From);
            Assert.Equal(new DateTime(2026, 10, 1), calendar.To);
            await dispatcher.InvokeAsync(() => calendar.Empty = true);
            Assert.Empty((await Query(new(1, 2026, 9))).Entries);
            Assert.True(server.IsRunning);
        }
        finally
        {
            dispatcher.BeginInvokeShutdown(DispatcherPriority.Send);
            Assert.True(thread.Join(TimeSpan.FromSeconds(5)));
        }
    }

    sealed class Calendar(Dispatcher dispatcher) : IJtsaCalendarPluginContext
    {
        public DateTime From, To;
        public bool Empty;
        public IReadOnlyList<CalendarEntryInfo> GetCalendarEntries(DateTime from, DateTime toExclusive)
        {
            Assert.True(dispatcher.CheckAccess());
            From = from; To = toExclusive;
            return Empty ? [] : [new(7, new DateTime(2026, 9, 27), TimeSpan.FromHours(21), "内容", "", "ゲーム", "", "確定タイトル")];
        }
    }
}
