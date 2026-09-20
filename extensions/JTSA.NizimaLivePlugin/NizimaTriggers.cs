using JTSA.Plugin.Abstractions;

namespace JTSA.NizimaLivePlugin;

public sealed record NizimaChoice(string Id, string Label);

public static class NizimaTriggerTypes
{
    public const string ChannelPoint = "ChannelPoint";
    public const string Chat = "Chat";
    public const string FirstChat = "FirstChat";
    public const string Follow = "Follow";
    public const string Raid = "Raid";
    public const string Subscribe = "Subscribe";
    public const string Bits = "Bits";
    public const string Hourly = "Hourly";
    public const string ScheduledTime = "ScheduledTime";
    public const string AdStart = "AdStart";
    public const string AdEnd = "AdEnd";
    public const string AdUpcoming = "AdUpcoming";
    public const string ObsStreamStart = "ObsStreamStart";

    public static IReadOnlyList<NizimaChoice> Choices { get; } =
    [
        new(ChannelPoint, "チャンネルポイント"),
        new(Chat, "チャット"),
        new(FirstChat, "初回チャット"),
        new(Follow, "フォロー"),
        new(Raid, "レイド"),
        new(Subscribe, "サブスク"),
        new(Bits, "Bits"),
        new(Hourly, "毎時"),
        new(ScheduledTime, "指定時刻"),
        new(AdStart, "広告開始"),
        new(AdEnd, "広告終了"),
        new(AdUpcoming, "広告予告"),
        new(ObsStreamStart, "OBS配信開始")
    ];

    public static string LabelOf(string id) =>
        Choices.FirstOrDefault(item => item.Id == id)?.Label ?? id;
}

public static class NizimaTriggerCommands
{
    public const string ChangeModel = "ChangeModel";
    public const string AddModel = "AddModel";
    public const string TriggerHotkey = "TriggerHotkey";
    public const string ExpressionOn = "ExpressionOn";
    public const string ExpressionOff = "ExpressionOff";
    public const string StartMotion = "StartMotion";
    public const string StopMotion = "StopMotion";
    public const string AddItem = "AddItem";
    public const string RemoveItem = "RemoveItem";
    public const string MoveModel = "MoveModel";
    public const string SetModelColor = "SetModelColor";
    public const string EffectOn = "EffectOn";
    public const string EffectOff = "EffectOff";
    public const string RawJson = "RawJson";

    public static IReadOnlyList<NizimaChoice> Choices { get; } =
    [
        new(ChangeModel, "モデルを切り替え"),
        new(AddModel, "モデルを追加"),
        new(TriggerHotkey, "ホットキーを実行"),
        new(ExpressionOn, "表情をオン"),
        new(ExpressionOff, "表情をオフ"),
        new(StartMotion, "モーションを開始"),
        new(StopMotion, "モーションを停止"),
        new(AddItem, "アイテムを追加"),
        new(RemoveItem, "アイテムを削除"),
        new(MoveModel, "モデルを移動"),
        new(SetModelColor, "モデルの色を変更"),
        new(EffectOn, "エフェクトをオン"),
        new(EffectOff, "エフェクトをオフ"),
        new(RawJson, "生JSONを送信")
    ];

    public static string LabelOf(string id) =>
        Choices.FirstOrDefault(item => item.Id == id)?.Label ?? id;
}

public sealed class NizimaTriggerCommandExtra
{
    public double X { get; set; }
    public double Y { get; set; }
    public double Rotation { get; set; }
    public double Size { get; set; } = 1;
    public bool Relative { get; set; } = true;
    public int DelayMs { get; set; }
    public int R { get; set; } = 255;
    public int G { get; set; } = 255;
    public int B { get; set; } = 255;
    public int A { get; set; } = 255;
    public bool UseScreen { get; set; }
    public string RawJson { get; set; } = "";
    public string ModelPath { get; set; } = "";
    public string ItemPath { get; set; } = "";
}

public sealed class NizimaTriggerRule
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public bool IsEnabled { get; set; } = true;
    public string TriggerType { get; set; } = NizimaTriggerTypes.ChannelPoint;
    public string TriggerValue { get; set; } = "";
    public string CommandType { get; set; } = NizimaTriggerCommands.ExpressionOn;
    public string CommandValue { get; set; } = "";
    public string ModelId { get; set; } = "";
    public string SceneId { get; set; } = "";
    public NizimaTriggerCommandExtra Extra { get; set; } = new();
}

public static class NizimaTriggerRuleSummary
{
    public static string Format(
        NizimaTriggerRule rule,
        NizimaRuleCatalogs? catalogs = null,
        IReadOnlyList<ChannelPointRewardInfo>? channelPoints = null)
    {
        catalogs ??= NizimaRuleCatalogs.Empty;
        var enabled = rule.IsEnabled ? "ON" : "OFF";
        var trigger = NizimaTriggerTypes.LabelOf(rule.TriggerType);
        if (!string.IsNullOrWhiteSpace(rule.TriggerValue))
        {
            var triggerDetail = rule.TriggerType == NizimaTriggerTypes.ChannelPoint
                ? NizimaChannelPointCatalog.TitleOf(channelPoints, rule.TriggerValue)
                : rule.TriggerValue;
            trigger += $":{triggerDetail}";
        }

        var command = NizimaTriggerCommands.LabelOf(rule.CommandType);
        var value = CommandDisplay(rule, catalogs);
        if (!string.IsNullOrWhiteSpace(value))
            command += $" {value}";

        var text = $"{enabled} {trigger} → {command}";
        if (!string.IsNullOrWhiteSpace(rule.ModelId))
            text += $"（モデル {DisplayName(catalogs.ModelsOnScreen, rule.ModelId) ?? rule.ModelId}）";
        if (!string.IsNullOrWhiteSpace(rule.SceneId) &&
            rule.CommandType is NizimaTriggerCommands.AddModel or NizimaTriggerCommands.AddItem)
            text += $"（シーン {rule.SceneId}）";
        return text;
    }

    private static string CommandDisplay(NizimaTriggerRule rule, NizimaRuleCatalogs catalogs)
    {
        var raw = rule.CommandValue;
        if (string.IsNullOrWhiteSpace(raw))
        {
            raw = rule.CommandType switch
            {
                NizimaTriggerCommands.ChangeModel or NizimaTriggerCommands.AddModel => rule.Extra.ModelPath,
                NizimaTriggerCommands.AddItem => rule.Extra.ItemPath,
                _ => ""
            };
        }

        if (string.IsNullOrWhiteSpace(raw))
            return "";

        var catalog = rule.CommandType switch
        {
            NizimaTriggerCommands.ExpressionOn or NizimaTriggerCommands.ExpressionOff => catalogs.Expressions,
            NizimaTriggerCommands.StartMotion or NizimaTriggerCommands.StopMotion => catalogs.Motions,
            NizimaTriggerCommands.ChangeModel or NizimaTriggerCommands.AddModel => catalogs.RegisteredModels,
            NizimaTriggerCommands.AddItem => catalogs.RegisteredItems,
            NizimaTriggerCommands.RemoveItem => catalogs.ItemsOnScreen,
            NizimaTriggerCommands.EffectOn or NizimaTriggerCommands.EffectOff => catalogs.EffectGroups,
            _ => null
        };
        return DisplayName(catalog, raw) ?? raw;
    }

    private static string? DisplayName(IReadOnlyList<NizimaNamedOption>? catalog, string path) =>
        catalog?.FirstOrDefault(item => item.Path == path)?.Name;
}

public static class NizimaTriggerMatcher
{
    public static bool Matches(NizimaTriggerRule rule, string triggerType, string value)
    {
        if (!rule.IsEnabled)
            return false;

        if (!string.Equals(rule.TriggerType, triggerType, StringComparison.Ordinal))
            return false;

        return triggerType switch
        {
            NizimaTriggerTypes.ChannelPoint =>
                !string.IsNullOrWhiteSpace(rule.TriggerValue) &&
                string.Equals(value, rule.TriggerValue, StringComparison.OrdinalIgnoreCase),
            NizimaTriggerTypes.Chat =>
                !string.IsNullOrWhiteSpace(rule.TriggerValue) &&
                value.Contains(rule.TriggerValue, StringComparison.OrdinalIgnoreCase),
            NizimaTriggerTypes.ScheduledTime =>
                string.Equals(value, rule.TriggerValue, StringComparison.Ordinal),
            NizimaTriggerTypes.AdUpcoming =>
                string.Equals(value, rule.TriggerValue, StringComparison.Ordinal),
            NizimaTriggerTypes.ObsStreamStart =>
                string.Equals(value, rule.TriggerValue, StringComparison.OrdinalIgnoreCase),
            _ => true
        };
    }
}

public sealed class NizimaLiveSettings
{
    public string WebSocketUrl { get; set; } = NizimaProtocol.DefaultWebSocketUrl;
    public string AuthToken { get; set; } = "";
    public bool AutoConnect { get; set; } = true;
    public List<NizimaTriggerRule> Rules { get; set; } = [];
}

public static class NizimaChangeModelRequest
{
    public static object Create(string modelId, string modelPath) => new
    {
        ModelId = modelId,
        ModelPath = modelPath
    };
}

public sealed record NizimaNamedOption(string Name, string Path)
{
    public string Label => string.IsNullOrWhiteSpace(Name) ? Path : Name;

    public static IReadOnlyList<NizimaNamedOption> FromArray(
        System.Text.Json.JsonElement data,
        string arrayName,
        string nameProperty,
        string pathProperty)
    {
        if (data.ValueKind != System.Text.Json.JsonValueKind.Object ||
            !data.TryGetProperty(arrayName, out var array) ||
            array.ValueKind != System.Text.Json.JsonValueKind.Array)
            return [];

        var list = new List<NizimaNamedOption>();
        foreach (var item in array.EnumerateArray())
        {
            var path = item.TryGetProperty(pathProperty, out var pathNode) ? pathNode.GetString() : null;
            if (string.IsNullOrWhiteSpace(path))
                continue;
            var name = item.TryGetProperty(nameProperty, out var nameNode) ? nameNode.GetString() ?? "" : "";
            list.Add(new NizimaNamedOption(name, path));
        }

        return list;
    }

    public static IReadOnlyList<NizimaNamedOption> FromScenes(System.Text.Json.JsonElement data)
    {
        if (!data.TryGetProperty("Scenes", out var array) ||
            array.ValueKind != System.Text.Json.JsonValueKind.Array)
            return [];

        var list = new List<NizimaNamedOption>();
        foreach (var item in array.EnumerateArray())
        {
            var sceneId = item.TryGetProperty("SceneId", out var idNode) ? idNode.GetString() : null;
            if (string.IsNullOrWhiteSpace(sceneId))
                continue;
            list.Add(new NizimaNamedOption($"シーン {sceneId}", sceneId));
        }

        return list;
    }
}
