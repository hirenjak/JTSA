using System.Text.Json;
using JTSA.Utility;
using Xunit;

namespace JTSA.Tests;

public class StreamExpansionClipTests
{
    [Theory]
    [InlineData(" @TwitchDev ", "twitchdev")]
    [InlineData("user_123", "user_123")]
    public void LoginAcceptsHandles(string input, string expected) =>
        Assert.Equal(expected, StreamExpansionClipService.NormalizeLogin(input));

    [Theory]
    [InlineData("")]
    [InlineData("https://twitch.tv/user")]
    [InlineData("表示名")]
    [InlineData("user&first=100")]
    public void LoginRejectsInvalidInput(string input) =>
        Assert.Throws<ArgumentException>(() => StreamExpansionClipService.NormalizeLogin(input));

    [Fact]
    public void PlaybackReplacesPreviousClipExpiresAndStaysSeparateFromImages()
    {
        StreamExpansionClipOverlay.ShowClip("https://cdn.example/first.mp4", 30);
        using var first = JsonDocument.Parse(StreamExpansionClipOverlay.CreateJson());
        var firstId = first.RootElement.GetProperty("clip").GetProperty("id").GetString();
        StreamExpansionClipOverlay.ShowClip("https://cdn.example/second.mp4", 45);
        using var second = JsonDocument.Parse(StreamExpansionClipOverlay.CreateJson());
        var clip = second.RootElement.GetProperty("clip");
        Assert.Equal("https://cdn.example/second.mp4", clip.GetProperty("videoUrl").GetString());
        Assert.NotEqual(firstId, clip.GetProperty("id").GetString());
        Assert.InRange(clip.GetProperty("remainingMs").GetDouble(), 45000, 50000);
        using var images = JsonDocument.Parse(StreamExpansionOverlayService.CreateJson());
        Assert.False(images.RootElement.TryGetProperty("clip", out _));
        using var expired = JsonDocument.Parse(StreamExpansionClipOverlay.CreateJson(DateTime.UtcNow.AddMinutes(10)));
        Assert.Equal(JsonValueKind.Null, expired.RootElement.GetProperty("clip").ValueKind);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void InvalidDurationCannotCreatePlayback(double duration) =>
        Assert.Throws<ArgumentException>(() =>
            StreamExpansionClipOverlay.ShowClip("https://cdn.example/clip.mp4", duration));

    [Fact]
    public void PlaybackResponseSelectsHighestLandscapeQualityAndAddsToken()
    {
        using var response = JsonDocument.Parse("""
        [{"data":{"clip":{"playbackAccessToken":{"signature":"a+b","value":"{\"x\":1}"},"assets":[
          {"aspectRatio":1.777,"videoQualities":[
            {"quality":"360","sourceURL":"https://cdn.example/360.mp4"},
            {"quality":"1080","sourceURL":"https://cdn.example/1080.mp4"}]},
          {"aspectRatio":0.5625,"videoQualities":[
            {"quality":"1920","sourceURL":"https://cdn.example/portrait.mp4"}]}
        ]}}}]
        """);

        var url = StreamExpansionClipService.ResolveVideoUrl(response.RootElement);

        Assert.Equal("https://cdn.example/1080.mp4?sig=a%2Bb&token=%7B%22x%22%3A1%7D", url);
    }

    [Fact]
    public void MissingClipCannotResolvePlaybackUrl()
    {
        using var response = JsonDocument.Parse("[{\"data\":{\"clip\":null}}]");
        Assert.Throws<InvalidOperationException>(() =>
            StreamExpansionClipService.ResolveVideoUrl(response.RootElement));
    }
}
