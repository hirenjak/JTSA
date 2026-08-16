using System.Net;
using System.Net.Http;
using JTSA.Utility;
using Xunit;

namespace JTSA.Tests;

public class BouyomiChanClientTests
{
    [Fact]
    public async Task SpeakAsyncSendsEncodedTextToTalkEndpoint()
    {
        HttpRequestMessage? captured = null;
        var handler = new StubHandler(request =>
        {
            captured = request;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        });
        var client = new BouyomiChanClient(new HttpClient(handler));

        await client.SpeakAsync("http://localhost:50080", "こんにちは & hello");

        Assert.NotNull(captured);
        Assert.Equal(HttpMethod.Get, captured.Method);
        Assert.Equal(
            "http://localhost:50080/Talk?text=%E3%81%93%E3%82%93%E3%81%AB%E3%81%A1%E3%81%AF%20%26%20hello",
            captured.RequestUri!.AbsoluteUri);
    }

    [Fact]
    public async Task SpeakAsyncDoesNotSendBlankText()
    {
        var calls = 0;
        var client = new BouyomiChanClient(new HttpClient(new StubHandler(request =>
        {
            calls++;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        })));

        await client.SpeakAsync(BouyomiChanClient.DefaultEndpoint, "  ");

        Assert.Equal(0, calls);
    }

    private sealed class StubHandler(
        Func<HttpRequestMessage, Task<HttpResponseMessage>> responseFactory) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) => responseFactory(request);
    }
}
