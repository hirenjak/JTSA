using NAudio.Wave;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;

namespace JTSA.Utility;

/// <summary>VOICEVOX Engineへ音声合成を依頼し、生成された音声を再生する。</summary>
public sealed class VoiceVoxClient
{
    public const string DefaultEndpoint = "http://localhost:50021/";
    public const int DefaultSpeakerId = 1;

    private readonly HttpClient httpClient;
    private readonly Func<byte[], CancellationToken, Task> playAudioAsync;
    private readonly SemaphoreSlim speechLock = new(1, 1);

    public VoiceVoxClient(HttpClient? httpClient = null)
        : this(httpClient ?? new HttpClient(), PlayWaveAsync)
    {
    }

    internal VoiceVoxClient(
        HttpClient httpClient,
        Func<byte[], CancellationToken, Task> playAudioAsync)
    {
        this.httpClient = httpClient;
        this.playAudioAsync = playAudioAsync;
    }

    /// <summary>Engineにインストールされている話者スタイルを取得する。</summary>
    public async Task<IReadOnlyList<VoiceVoxSpeakerStyle>> GetSpeakerStylesAsync(
        string endpoint,
        CancellationToken cancellationToken = default)
    {
        var uri = new Uri(ValidateEndpoint(endpoint), "speakers");
        using var response = await httpClient.GetAsync(uri, cancellationToken);
        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

        var styles = new List<VoiceVoxSpeakerStyle>();
        foreach (var speaker in document.RootElement.EnumerateArray())
        {
            var speakerName = speaker.GetProperty("name").GetString() ?? "不明な話者";
            foreach (var style in speaker.GetProperty("styles").EnumerateArray())
            {
                var styleName = style.GetProperty("name").GetString() ?? "不明なスタイル";
                var id = style.GetProperty("id").GetInt32();
                styles.Add(new VoiceVoxSpeakerStyle(id, $"{speakerName}（{styleName}）"));
            }
        }

        return styles;
    }

    public async Task SpeakAsync(
        string endpoint,
        int speakerId,
        string text,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(text)) return;
        if (speakerId < 0) throw new ArgumentOutOfRangeException(nameof(speakerId), "話者IDは0以上で指定してください。");

        var baseUri = ValidateEndpoint(endpoint);
        await speechLock.WaitAsync(cancellationToken);
        try
        {
            var queryUri = CreateApiUri(baseUri, "audio_query", speakerId, text);
            using var queryResponse = await httpClient.PostAsync(queryUri, content: null, cancellationToken);
            queryResponse.EnsureSuccessStatusCode();
            var audioQuery = await queryResponse.Content.ReadAsStringAsync(cancellationToken);

            var synthesisUri = CreateApiUri(baseUri, "synthesis", speakerId);
            using var content = new StringContent(audioQuery, Encoding.UTF8, "application/json");
            using var synthesisResponse = await httpClient.PostAsync(synthesisUri, content, cancellationToken);
            synthesisResponse.EnsureSuccessStatusCode();
            var wave = await synthesisResponse.Content.ReadAsByteArrayAsync(cancellationToken);
            await playAudioAsync(wave, cancellationToken);
        }
        finally
        {
            speechLock.Release();
        }
    }

    internal static Uri ValidateEndpoint(string endpoint)
    {
        var value = string.IsNullOrWhiteSpace(endpoint) ? DefaultEndpoint : endpoint.Trim();
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            throw new ArgumentException("VOICEVOX EngineのURLが正しくありません。", nameof(endpoint));
        }

        return uri.AbsoluteUri.EndsWith('/') ? uri : new Uri(uri.AbsoluteUri + "/");
    }

    private static Uri CreateApiUri(Uri baseUri, string path, int speakerId, string? text = null)
    {
        var query = text is null
            ? $"speaker={speakerId}"
            : $"speaker={speakerId}&text={Uri.EscapeDataString(text)}";
        return new UriBuilder(new Uri(baseUri, path)) { Query = query }.Uri;
    }

    private static async Task PlayWaveAsync(byte[] wave, CancellationToken cancellationToken)
    {
        using var stream = new MemoryStream(wave, writable: false);
        using var reader = new WaveFileReader(stream);
        using var output = new WaveOutEvent();
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        output.PlaybackStopped += (_, e) =>
        {
            if (e.Exception is not null) completion.TrySetException(e.Exception);
            else completion.TrySetResult();
        };
        using var registration = cancellationToken.Register(() =>
        {
            output.Stop();
            completion.TrySetCanceled(cancellationToken);
        });
        output.Init(reader);
        output.Play();
        await completion.Task;
    }
}

public sealed record VoiceVoxSpeakerStyle(int Id, string DisplayName);
