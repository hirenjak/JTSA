using System.Net;
using System.Net.Http;
using System.Text;
using JTSA.Utility;
using Xunit;

namespace JTSA.Tests;

public sealed class TwitchUsersClientTests
{
    [Fact]
    public async Task GetByIdsAsync_RequestsAtMostOneHundredIdsPerPage()
    {
        var requestSizes = new List<int>();
        using var http = new HttpClient(new StubHandler(request =>
        {
            requestSizes.Add(request.RequestUri!.Query.Split("id=").Length - 1);
            Assert.Equal("token", request.Headers.Authorization?.Parameter);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    """{"data":[{"id":"1","login":"one","display_name":"One","profile_image_url":"https://example.test/one.png"}]}""",
                    Encoding.UTF8, "application/json")
            };
        }));

        var result = await new TwitchUsersClient(http).GetByIdsAsync(
            Enumerable.Range(1, 101).Select(x => x.ToString()), "token", "client");

        Assert.True(result.IsSuccess);
        Assert.Equal(new[] { 100, 1 }, requestSizes);
        Assert.Equal("https://example.test/one.png", result.Data![0].ProfileImageUrl);
    }

    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(respond(request));
    }
}
