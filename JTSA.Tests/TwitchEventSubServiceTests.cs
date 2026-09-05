using System.Reflection;
using JTSA.Utility;
using TwitchLib.Api;
using TwitchLib.Api.Core.Enums;
using TwitchLib.Api.Core.Interfaces;
using TwitchLib.EventSub.Websockets;
using TwitchLib.EventSub.Websockets.Core.EventArgs;
using Xunit;

namespace JTSA.Tests;

public class TwitchEventSubServiceTests
{
    [Fact]
    public async Task ReconnectAfterRefresh_UsesNewTokenForEverySubscription()
    {
        var http = new RecordingHttpHandler();
        var api = new TwitchAPI(http: http);
        api.Settings.ClientId = "test-client";
        api.Settings.AccessToken = "expired-token";
        await using var service = new TwitchEventSubService(api, "account-a");

        await SimulateConnectionAsync(service, "session-before");
        Assert.Equal(5, http.Tokens.Count);
        Assert.All(http.Tokens, token => Assert.Equal("expired-token", token));
        http.Tokens.Clear();

        Assert.True(service.UpdateAccessToken("account-a", "refreshed-token"));
        await SimulateConnectionAsync(service, "session-after");

        Assert.Equal(5, http.Tokens.Count);
        Assert.All(http.Tokens, token => Assert.Equal("refreshed-token", token));
        Assert.Contains("TokenGeneration=1", service.GetTokenDiagnostics());
        Assert.DoesNotContain("refreshed-token", service.GetTokenDiagnostics());
        Assert.DoesNotContain("expired-token", service.GetTokenDiagnostics());
    }

    [Fact]
    public async Task RefreshForDifferentAccountOrEmptyToken_DoesNotReplaceCredentials()
    {
        var api = new TwitchAPI();
        api.Settings.AccessToken = "selected-account-token";
        await using var service = new TwitchEventSubService(api, "selected-account");

        Assert.False(service.UpdateAccessToken("primary-account", "primary-token"));
        Assert.False(service.UpdateAccessToken("selected-account", " "));
        Assert.Equal("selected-account-token", api.Settings.AccessToken);
        Assert.Contains("TokenGeneration=0", service.GetTokenDiagnostics());
    }

    [Fact]
    public async Task RequestedReconnect_DoesNotDuplicateSubscriptions()
    {
        var http = new RecordingHttpHandler();
        var api = new TwitchAPI(http: http);
        api.Settings.ClientId = "test-client";
        api.Settings.AccessToken = "token";
        await using var service = new TwitchEventSubService(api, "account-a");

        await SimulateConnectionAsync(service, "session-before");
        http.Tokens.Clear();
        service.UpdateAccessToken("account-a", "new-token");
        await SimulateConnectionAsync(service, "session-migrated", requestedReconnect: true);

        Assert.Empty(http.Tokens);
    }

    // 接続イベントだけを再現し、実際の購読API要求は偽HTTPで検証する。
    private static async Task SimulateConnectionAsync(
        TwitchEventSubService service, string sessionId, bool requestedReconnect = false)
    {
        var client = (EventSubWebsocketClient)typeof(TwitchEventSubService)
            .GetField("eventSubClient", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(service)!;
        typeof(EventSubWebsocketClient).GetProperty(nameof(EventSubWebsocketClient.SessionId))!
            .SetValue(client, sessionId);
        var connected = typeof(TwitchEventSubService)
            .GetMethod("OnWebsocketConnected", BindingFlags.Instance | BindingFlags.NonPublic)!;
        await (Task)connected.Invoke(service,
            new object?[] { null, new WebsocketConnectedArgs { IsRequestedReconnect = requestedReconnect } })!;
    }

    private sealed class RecordingHttpHandler : IHttpCallHandler
    {
        public List<string> Tokens { get; } = new();

        public Task<KeyValuePair<int, string>> GeneralRequestAsync(
            string url, string method, string payload, ApiVersion api, string clientId, string accessToken)
        {
            Tokens.Add(accessToken);
            return Task.FromResult(new KeyValuePair<int, string>(202,
                """{"data":[{"id":"test-subscription","status":"enabled"}],"total":1,"total_cost":0,"max_total_cost":10}"""));
        }

        public Task PutBytesAsync(string url, byte[] data) => throw new NotSupportedException();
        public Task<int> RequestReturnResponseCodeAsync(
            string url, string method, List<KeyValuePair<string, string>> headers) => throw new NotSupportedException();
    }
}
