using System.Net.Http;

namespace JTSA.Utility;

/// <summary>棒読みちゃんのHTTP連携へ読み上げを依頼する。</summary>
public sealed class BouyomiChanClient
{
    public const string DefaultEndpoint = "http://localhost:50080/";

    private readonly HttpClient httpClient;

    public BouyomiChanClient(HttpClient? httpClient = null)
    {
        this.httpClient = httpClient ?? new HttpClient();
    }

    public async Task SpeakAsync(
        string endpoint,
        string text,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(text)) return;

        using var response = await httpClient.GetAsync(
            CreateTalkUri(endpoint, text),
            cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    internal static Uri CreateTalkUri(string endpoint, string text)
    {
        var value = string.IsNullOrWhiteSpace(endpoint) ? DefaultEndpoint : endpoint.Trim();
        if (!Uri.TryCreate(value, UriKind.Absolute, out var baseUri) ||
            (baseUri.Scheme != Uri.UriSchemeHttp && baseUri.Scheme != Uri.UriSchemeHttps))
        {
            throw new ArgumentException("棒読みちゃんのURLが正しくありません。", nameof(endpoint));
        }

        var normalized = baseUri.AbsoluteUri.EndsWith('/')
            ? baseUri
            : new Uri(baseUri.AbsoluteUri + "/");
        var talkUri = new Uri(normalized, "Talk");
        return new UriBuilder(talkUri)
        {
            Query = $"text={Uri.EscapeDataString(text)}"
        }.Uri;
    }
}
