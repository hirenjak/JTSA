using System.Net;
using System.Net.Http;
using System.Text;
using JTSA.Utility;
using Xunit;

namespace JTSA.Tests;

public sealed class TwitchChattersClientTests
{
    [Fact]
    public async Task GetAsync_LoadsEveryPageWithBroadcasterAuthorization()
    {
        var requests = new List<Uri>();
        using var http = new HttpClient(new StubHandler(request =>
        {
            requests.Add(request.RequestUri!);
            Assert.Equal("Bearer", request.Headers.Authorization?.Scheme);
            Assert.Equal("test-token", request.Headers.Authorization?.Parameter);
            Assert.Equal("test-client", Assert.Single(request.Headers.GetValues("Client-Id")));
            var body = requests.Count == 1
                ? """{"data":[{"user_id":"1","user_login":"first","user_name":"First"}],"pagination":{"cursor":"next-page"}}"""
                : """{"data":[{"user_id":"2","user_login":"second","user_name":"Second"}],"pagination":{}}""";
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json")
            };
        }));

        var result = await new TwitchChattersClient(http).GetAsync("broadcaster", "test-token", "test-client");

        Assert.True(result.IsSuccess);
        Assert.Equal(new[] { "1", "2" }, result.Data!.Select(x => x.UserId));
        Assert.All(requests, uri =>
        {
            Assert.Contains("broadcaster_id=broadcaster", uri.Query);
            Assert.Contains("moderator_id=broadcaster", uri.Query);
        });
        Assert.Contains("after=next-page", requests[1].Query);
    }

    [Fact]
    public async Task GetAsync_UnauthorizedDoesNotReturnPartialUsers()
    {
        var count = 0;
        using var http = new HttpClient(new StubHandler(_ =>
        {
            count++;
            return count == 1
                ? new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(
                        """{"data":[{"user_id":"1"}],"pagination":{"cursor":"next"}}""",
                        Encoding.UTF8, "application/json")
                }
                : new HttpResponseMessage(HttpStatusCode.Unauthorized);
        }));

        var result = await new TwitchChattersClient(http).GetAsync("broadcaster", "token", "client");

        Assert.False(result.IsSuccess);
        Assert.Null(result.Data);
        Assert.Equal(TwitchApiErrorKind.Unauthorized, result.ErrorKind);
    }

    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(respond(request));
    }
}
