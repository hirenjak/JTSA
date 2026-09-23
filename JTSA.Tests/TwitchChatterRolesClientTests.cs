using System.Net;
using System.Net.Http;
using System.Text;
using JTSA.Utility;
using Xunit;

namespace JTSA.Tests;

public sealed class TwitchChatterRolesClientTests
{
    [Fact]
    public async Task GetAsync_LoadsAllModeratorAndVipPages()
    {
        var requests = new List<Uri>();
        using var http = new HttpClient(new StubHandler(request =>
        {
            requests.Add(request.RequestUri!);
            Assert.Equal("token", request.Headers.Authorization?.Parameter);
            Assert.Equal("client", Assert.Single(request.Headers.GetValues("Client-Id")));
            var path = request.RequestUri!.AbsolutePath;
            var query = request.RequestUri.Query;
            var body = path.EndsWith("moderators", StringComparison.Ordinal)
                ? query.Contains("after=next", StringComparison.Ordinal)
                    ? """{"data":[{"user_id":"mod2"}],"pagination":{}}"""
                    : """{"data":[{"user_id":"mod1"}],"pagination":{"cursor":"next"}}"""
                : """{"data":[{"user_id":"vip1"}],"pagination":{}}""";
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json")
            };
        }));

        var result = await new TwitchChatterRolesClient(http).GetAsync("broadcaster", "token", "client");

        Assert.True(result.IsSuccess);
        Assert.Equal(["mod1", "mod2"], result.Data!.ModeratorIds.OrderBy(x => x));
        Assert.Equal(["vip1"], result.Data.VipIds.OrderBy(x => x));
        Assert.Equal(3, requests.Count);
        Assert.All(requests, uri => Assert.Contains("broadcaster_id=broadcaster", uri.Query));
    }

    [Fact]
    public async Task GetAsync_ForbiddenDoesNotMisclassifyUsersAsViewers()
    {
        using var http = new HttpClient(new StubHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.Forbidden)));

        var result = await new TwitchChatterRolesClient(http).GetAsync("broadcaster", "token", "client");

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
