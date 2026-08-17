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

    /// <summary>VOICEVOX標準話者のスタイル一覧。</summary>
    public static IReadOnlyList<VoiceVoxSpeakerStyle> BuiltInSpeakerStyles { get; } =
    [
        new(2, "四国めたん（ノーマル）"), new(0, "四国めたん（あまあま）"),
        new(6, "四国めたん（ツンツン）"), new(4, "四国めたん（セクシー）"),
        new(36, "四国めたん（ささやき）"), new(37, "四国めたん（ヒソヒソ）"),
        new(3, "ずんだもん（ノーマル）"), new(1, "ずんだもん（あまあま）"),
        new(7, "ずんだもん（ツンツン）"), new(5, "ずんだもん（セクシー）"),
        new(22, "ずんだもん（ささやき）"), new(38, "ずんだもん（ヒソヒソ）"),
        new(75, "ずんだもん（ヘロヘロ）"), new(76, "ずんだもん（なみだめ）"),
        new(8, "春日部つむぎ（ノーマル）"), new(10, "雨晴はう（ノーマル）"),
        new(9, "波音リツ（ノーマル）"), new(65, "波音リツ（クイーン）"),
        new(11, "玄野武宏（ノーマル）"), new(39, "玄野武宏（喜び）"),
        new(40, "玄野武宏（ツンギレ）"), new(41, "玄野武宏（悲しみ）"),
        new(12, "白上虎太郎（ふつう）"), new(32, "白上虎太郎（わーい）"),
        new(33, "白上虎太郎（びくびく）"), new(34, "白上虎太郎（おこ）"),
        new(35, "白上虎太郎（びえーん）"), new(13, "青山龍星（ノーマル）"),
        new(81, "青山龍星（熱血）"), new(82, "青山龍星（不機嫌）"),
        new(83, "青山龍星（喜び）"), new(84, "青山龍星（しっとり）"),
        new(85, "青山龍星（かなしみ）"), new(86, "青山龍星（囁き）"),
        new(14, "冥鳴ひまり（ノーマル）"), new(16, "九州そら（ノーマル）"),
        new(15, "九州そら（あまあま）"), new(18, "九州そら（ツンツン）"),
        new(17, "九州そら（セクシー）"), new(19, "九州そら（ささやき）"),
        new(20, "もち子さん（ノーマル）"), new(66, "もち子さん（セクシー／あん子）"),
        new(77, "もち子さん（泣き）"), new(78, "もち子さん（怒り）"),
        new(79, "もち子さん（喜び）"), new(80, "もち子さん（のんびり）"),
        new(21, "剣崎雌雄（ノーマル）"), new(23, "WhiteCUL（ノーマル）"),
        new(24, "WhiteCUL（たのしい）"), new(25, "WhiteCUL（かなしい）"),
        new(26, "WhiteCUL（びえーん）"), new(27, "後鬼（人間 ver.）"),
        new(28, "後鬼（ぬいぐるみ ver.）"), new(87, "後鬼（人間・怒り ver.）"),
        new(88, "後鬼（鬼 ver.）"), new(29, "No.7（ノーマル）"),
        new(30, "No.7（アナウンス）"), new(31, "No.7（読み聞かせ）"),
        new(42, "ちび式じい（ノーマル）"), new(43, "櫻歌ミコ（ノーマル）"),
        new(44, "櫻歌ミコ（第二形態）"), new(45, "櫻歌ミコ（ロリ）"),
        new(46, "小夜／SAYO（ノーマル）"), new(47, "ナースロボ タイプT（ノーマル）"),
        new(48, "ナースロボ タイプT（楽々）"), new(49, "ナースロボ タイプT（恐怖）"),
        new(50, "ナースロボ タイプT（内緒話）"), new(51, "聖騎士紅桜（ノーマル）"),
        new(52, "雀松朱司（ノーマル）"), new(53, "麒ヶ島宗麟（ノーマル）"),
        new(54, "春歌ナナ（ノーマル）"), new(55, "猫使アル（ノーマル）"),
        new(56, "猫使アル（おちつき）"), new(57, "猫使アル（うきうき）"),
        new(58, "猫使ビィ（ノーマル）"), new(59, "猫使ビィ（おちつき）"),
        new(60, "猫使ビィ（人見知り）"), new(61, "中国うさぎ（ノーマル）"),
        new(62, "中国うさぎ（おどろき）"), new(63, "中国うさぎ（こわがり）"),
        new(64, "中国うさぎ（へろへろ）"), new(67, "栗田まろん（ノーマル）"),
        new(68, "あいえるたん（ノーマル）"), new(69, "満別花丸（ノーマル）"),
        new(70, "満別花丸（元気）"), new(71, "満別花丸（ささやき）"),
        new(72, "満別花丸（ぶりっ子）"), new(73, "満別花丸（ボーイ）"),
        new(74, "琴詠ニア（ノーマル）"), new(89, "Voidoll（ノーマル）"),
        new(90, "ぞん子（ノーマル）"), new(91, "ぞん子（低血圧）"),
        new(92, "ぞん子（覚醒）"), new(93, "ぞん子（実況風）"),
        new(94, "中部つるぎ（ノーマル）"), new(95, "中部つるぎ（怒り）"),
        new(96, "中部つるぎ（ヒソヒソ）"), new(97, "中部つるぎ（おどおど）"),
        new(98, "中部つるぎ（絶望と敗北）")
    ];

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
