using System.Net;
using System.Net.Http;
using System.Text;
using JTSA.Utility;
using Xunit;

namespace JTSA.Tests;

public class VoiceVoxClientTests
{
    [Fact]
    public async Task GetSpeakerStylesAsyncReturnsNamesAndStyleIds()
    {
        HttpRequestMessage? captured = null;
        var json = """
            [
              { "name": "ずんだもん", "styles": [
                { "name": "ノーマル", "id": 3 },
                { "name": "あまあま", "id": 1 }
              ]}
            ]
            """;
        var client = new VoiceVoxClient(
            new HttpClient(new StubHandler(request =>
            {
                captured = request;
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(json, Encoding.UTF8, "application/json")
                });
            })),
            (_, _) => Task.CompletedTask);

        var styles = await client.GetSpeakerStylesAsync("http://localhost:50021");

        Assert.Equal(HttpMethod.Get, captured!.Method);
        Assert.Equal("http://localhost:50021/speakers", captured.RequestUri!.AbsoluteUri);
        Assert.Collection(styles,
            style => Assert.Equal(new VoiceVoxSpeakerStyle(3, "ずんだもん（ノーマル）"), style),
            style => Assert.Equal(new VoiceVoxSpeakerStyle(1, "ずんだもん（あまあま）"), style));
    }

    [Fact]
    public async Task SpeakAsyncCreatesQuerySynthesizesAndPlaysWave()
    {
        var requests = new List<(HttpMethod Method, string Uri, string? Body)>();
        var played = Array.Empty<byte>();
        var handler = new StubHandler(async request =>
        {
            requests.Add((request.Method, request.RequestUri!.AbsoluteUri,
                request.Content is null ? null : await request.Content.ReadAsStringAsync()));
            return requests.Count == 1
                ? new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("{\"accent_phrases\":[]}", Encoding.UTF8, "application/json")
                }
                : new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new ByteArrayContent([1, 2, 3])
                };
        });
        var client = new VoiceVoxClient(
            new HttpClient(handler),
            (wave, _) =>
            {
                played = wave;
                return Task.CompletedTask;
            });

        await client.SpeakAsync("http://localhost:50021", 3, "こんにちは & hello");

        Assert.Equal(2, requests.Count);
        Assert.Equal(HttpMethod.Post, requests[0].Method);
        Assert.Equal(
            "http://localhost:50021/audio_query?speaker=3&text=%E3%81%93%E3%82%93%E3%81%AB%E3%81%A1%E3%81%AF%20%26%20hello",
            requests[0].Uri);
        Assert.Equal(HttpMethod.Post, requests[1].Method);
        Assert.Equal("http://localhost:50021/synthesis?speaker=3", requests[1].Uri);
        Assert.Equal("{\"accent_phrases\":[]}", requests[1].Body);
        Assert.Equal([1, 2, 3], played);
    }

    [Fact]
    public async Task SpeakAsyncDoesNotSendBlankText()
    {
        var calls = 0;
        var client = new VoiceVoxClient(
            new HttpClient(new StubHandler(request =>
            {
                calls++;
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
            })),
            (_, _) => Task.CompletedTask);

        await client.SpeakAsync(VoiceVoxClient.DefaultEndpoint, 1, "  ");

        Assert.Equal(0, calls);
    }

    [Fact]
    public async Task SpeakAsyncRejectsNegativeSpeakerId()
    {
        var client = new VoiceVoxClient(
            new HttpClient(new StubHandler(_ =>
                Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)))),
            (_, _) => Task.CompletedTask);

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            client.SpeakAsync(VoiceVoxClient.DefaultEndpoint, -1, "test"));
    }

    private sealed class StubHandler(
        Func<HttpRequestMessage, Task<HttpResponseMessage>> responseFactory) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) => responseFactory(request);
    }
}
