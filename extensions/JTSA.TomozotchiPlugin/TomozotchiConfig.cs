using System.IO;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace JTSA.TomozotchiPlugin;

/// <summary>ゲージ定義。項目名は tamagotchi-twitch の game_config.json と同じ。</summary>
public sealed class StatDefinition
{
    public string Name { get; set; } = "";
    public int MaxHearts { get; set; } = 3;
    public int? InitialHearts { get; set; }
    /// <summary>減少間隔（分）。0 は減らない。</summary>
    public int DecreaseInterval { get; set; } = 30;
    public string MarkFull { get; set; } = "♥";
    public int? Order { get; set; }

    [JsonIgnore]
    public int Initial => Math.Clamp(InitialHearts ?? MaxHearts, 0, MaxHearts);

    public StatDefinition Clone() => (StatDefinition)MemberwiseClone();

    /// <summary>不正なら理由を返す。</summary>
    public string? Validate()
    {
        if (string.IsNullOrWhiteSpace(Name)) return "ゲージ名を入力してください";
        if (MaxHearts <= 0) return "最大値は正の整数である必要があります";
        if (InitialHearts is { } initial && (initial < 0 || initial > MaxHearts))
            return $"初期値は0以上、最大値({MaxHearts})以下である必要があります";
        if (DecreaseInterval < 0) return "間隔は0以上の整数（分）である必要があります（0=減少しない）";
        return null;
    }
}

public static class RewardActionTypes
{
    public const string Modify = "modify";
    public const string AddStat = "addStat";
    public const string RemoveStat = "removeStat";
}

public sealed class RewardAction
{
    public string RewardId { get; set; } = "";
    public string Action { get; set; } = RewardActionTypes.Modify;
    public string? Stat { get; set; }
    public int? Amount { get; set; }
    public StatDefinition? StatDef { get; set; }
}

public sealed class ChannelPointHooks
{
    public List<RewardAction> RewardActions { get; set; } = [];

    // todoRewardIds などチャネポ状況表示用の設定は、移行時に失わないよう保持だけする
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? Extra { get; set; }
}

public sealed class GameConfig
{
    public List<StatDefinition> Stats { get; set; } = [];
    public ChannelPointHooks? ChannelPoints { get; set; }

    public static GameConfig CreateDefault() => new()
    {
        Stats =
        [
            new() { Name = "おなか", MaxHearts = 5, InitialHearts = 5, DecreaseInterval = 30, MarkFull = "♥" },
            new() { Name = "きんとれ", MaxHearts = 6, InitialHearts = 6, DecreaseInterval = 30, MarkFull = "💪" },
            new() { Name = "おみず", MaxHearts = 4, InitialHearts = 4, DecreaseInterval = 30, MarkFull = "💧" }
        ],
        ChannelPoints = new()
    };

    /// <summary>order 順に並べ、名前のないゲージを除く。</summary>
    public void Normalize()
    {
        Stats = Stats
            .Where(stat => !string.IsNullOrWhiteSpace(stat.Name))
            .Select((stat, index) => (stat, index))
            .OrderBy(item => item.stat.Order ?? int.MaxValue)
            .ThenBy(item => item.index)
            .Select(item => item.stat)
            .ToList();
    }
}

public sealed class TomozotchiSettings
{
    public GameConfig Game { get; set; } = GameConfig.CreateDefault();
    public int OverlayX { get; set; } = 20;
    public int OverlayY { get; set; } = 20;
    public int OverlayWidth { get; set; } = 800;
    public int OverlayHeight { get; set; } = 220;
}

public static class TomozotchiStore
{
    public static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        NumberHandling = JsonNumberHandling.AllowReadingFromString,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        WriteIndented = true
    };

    public static TomozotchiSettings Load(string directory)
    {
        var path = Path.Combine(directory, "settings.json");
        TomozotchiSettings settings;
        try
        {
            settings = File.Exists(path)
                ? JsonSerializer.Deserialize<TomozotchiSettings>(File.ReadAllText(path), Options) ?? new()
                : new();
        }
        catch
        {
            settings = new();
        }
        settings.Game ??= GameConfig.CreateDefault();
        settings.Game.Normalize();
        settings.Game.ChannelPoints ??= new();
        return settings;
    }

    public static void Save(string directory, TomozotchiSettings settings)
    {
        Directory.CreateDirectory(directory);
        WriteOrdered(settings.Game);
        File.WriteAllText(Path.Combine(directory, "settings.json"), JsonSerializer.Serialize(settings, Options));
    }

    /// <summary>game_config.json / savedata/*.json 形式のファイルを読む。</summary>
    public static GameConfig ReadGameConfig(string path)
    {
        var config = JsonSerializer.Deserialize<GameConfig>(File.ReadAllText(path), Options)
            ?? throw new InvalidDataException("設定ファイルが空です");
        config.Normalize();
        return config;
    }

    public static string PresetDirectory(string dataDirectory) => Path.Combine(dataDirectory, "presets");

    public static IReadOnlyList<string> ListPresets(string dataDirectory)
    {
        var directory = PresetDirectory(dataDirectory);
        return Directory.Exists(directory)
            ? Directory.GetFiles(directory, "*.json").Select(Path.GetFileNameWithoutExtension).OfType<string>().Order().ToArray()
            : [];
    }

    public static string PresetPath(string dataDirectory, string name) =>
        Path.Combine(PresetDirectory(dataDirectory), name + ".json");

    /// <summary>プリセットはゲージ定義だけを保存する（元アプリのスロット保存と同じ）。</summary>
    public static void SavePreset(string dataDirectory, string name, IEnumerable<StatDefinition> stats)
    {
        if (string.IsNullOrWhiteSpace(name) || name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            throw new ArgumentException("ファイル名に使えない文字が含まれています");
        Directory.CreateDirectory(PresetDirectory(dataDirectory));
        var config = new GameConfig { Stats = stats.Select(stat => stat.Clone()).ToList() };
        WriteOrdered(config);
        File.WriteAllText(PresetPath(dataDirectory, name), JsonSerializer.Serialize(config, Options));
    }

    private static void WriteOrdered(GameConfig config)
    {
        for (var index = 0; index < config.Stats.Count; index++)
            config.Stats[index].Order = index;
    }
}
