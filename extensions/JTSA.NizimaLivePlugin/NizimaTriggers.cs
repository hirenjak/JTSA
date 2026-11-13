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
    public const string ExpressionOn = "ExpressionOn";
    public const string ExpressionOff = "ExpressionOff";
    public const string ExpressionToggle = "ExpressionToggle";
    public const string StartMotion = "StartMotion";
    public const string StopMotion = "StopMotion";
    public const string ItemExpressionOn = "ItemExpressionOn";
    public const string ItemExpressionOff = "ItemExpressionOff";
    public const string ItemExpressionToggle = "ItemExpressionToggle";

    public static IReadOnlyList<NizimaChoice> Choices { get; } =
    [
        new(ChangeModel, "モデルを切り替え"),
        new(ExpressionOn, "表情をオン"),
        new(ExpressionOff, "表情をオフ"),
        new(ExpressionToggle, "表情を切り替え"),
        new(StartMotion, "モーションを開始"),
        new(StopMotion, "モーションを停止"),
        new(ItemExpressionOn, "アイテムの表情をオン"),
        new(ItemExpressionOff, "アイテムの表情をオフ"),
        new(ItemExpressionToggle, "アイテムの表情を切り替え")
    ];

    public static string LabelOf(string id) =>
        Choices.FirstOrDefault(item => item.Id == id)?.Label ?? id;
}

public static class NizimaAutoOffUnits
{
    public const string Seconds = "Seconds";
    public const string Minutes = "Minutes";
    public const string Hours = "Hours";

    public static TimeSpan Max { get; } = TimeSpan.FromHours(24);

    public static IReadOnlyList<NizimaChoice> Choices { get; } =
    [
        new(Seconds, "秒"),
        new(Minutes, "分"),
        new(Hours, "時間")
    ];

    public static bool Supports(string? commandType) =>
        commandType is NizimaTriggerCommands.ExpressionOn or NizimaTriggerCommands.ItemExpressionOn;

    public static TimeSpan ToTimeSpan(int value, string? unit) =>
        unit switch
        {
            Minutes => TimeSpan.FromMinutes(value),
            Hours => TimeSpan.FromHours(value),
            _ => TimeSpan.FromSeconds(value)
        };

    public static string LabelOf(string? id) =>
        Choices.FirstOrDefault(item => item.Id == id)?.Label ?? "秒";
}

public sealed class NizimaTriggerCommandExtra
{
    public string ModelPath { get; set; } = "";
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
    // モデルから一覧を取れないときに表示する、保存時点の表情・モーション名。
    public string CommandValueName { get; set; } = "";
    public int AutoOffValue { get; set; }
    public string AutoOffUnit { get; set; } = NizimaAutoOffUnits.Seconds;
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
                : NizimaTriggerUi.FormatTriggerValueForSummary(rule.TriggerType, rule.TriggerValue);
            trigger += $":{triggerDetail}";
        }

        var command = NizimaTriggerCommands.LabelOf(rule.CommandType);
        var value = CommandDisplay(rule, catalogs);
        if (!string.IsNullOrWhiteSpace(value))
            command += $" {value}";
        if (NizimaAutoOffUnits.Supports(rule.CommandType) && rule.AutoOffValue > 0)
            command += $"（{rule.AutoOffValue}{NizimaAutoOffUnits.LabelOf(rule.AutoOffUnit)}後に解除）";

        var text = $"{enabled} {trigger} → {command}";
        if (!string.IsNullOrWhiteSpace(rule.ModelId))
        {
            var targetCatalog = NizimaCommandUi.ShowsItemTarget(rule.CommandType)
                ? catalogs.Live2DItems
                : catalogs.ModelsOnScreen;
            var targetLabel = NizimaCommandUi.ShowsItemTarget(rule.CommandType) ? "アイテム" : "モデル";
            text += $"（{targetLabel} {DisplayName(targetCatalog, rule.ModelId) ?? rule.ModelId}）";
        }
        return text;
    }

    private static string CommandDisplay(NizimaTriggerRule rule, NizimaRuleCatalogs catalogs)
    {
        var raw = rule.CommandValue;
        if (string.IsNullOrWhiteSpace(raw))
            raw = rule.CommandType == NizimaTriggerCommands.ChangeModel ? rule.Extra.ModelPath : "";

        if (string.IsNullOrWhiteSpace(raw))
            return "";

        var catalog = rule.CommandType switch
        {
            NizimaTriggerCommands.ExpressionOn or NizimaTriggerCommands.ExpressionOff
                or NizimaTriggerCommands.ExpressionToggle => catalogs.Expressions,
            NizimaTriggerCommands.ItemExpressionOn or NizimaTriggerCommands.ItemExpressionOff
                or NizimaTriggerCommands.ItemExpressionToggle =>
                catalogs.ItemExpressions.TryGetValue(rule.ModelId, out var itemExpressions) ? itemExpressions : null,
            NizimaTriggerCommands.StartMotion or NizimaTriggerCommands.StopMotion => catalogs.Motions,
            NizimaTriggerCommands.ChangeModel => catalogs.RegisteredModels,
            _ => null
        };
        return DisplayName(catalog, raw) ??
               (string.IsNullOrWhiteSpace(rule.CommandValueName) ? raw : rule.CommandValueName);
    }

    private static string? DisplayName(IReadOnlyList<NizimaNamedOption>? catalog, string path) =>
        catalog?.FirstOrDefault(item => string.Equals(item.Path, path, StringComparison.OrdinalIgnoreCase)) is { } match &&
        !string.IsNullOrWhiteSpace(match.Name)
            ? match.Name
            : null;
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

    public static IReadOnlyList<NizimaNamedOption> FromLive2DItems(System.Text.Json.JsonElement data)
    {
        if (!data.TryGetProperty("Items", out var array) ||
            array.ValueKind != System.Text.Json.JsonValueKind.Array)
            return [];

        var list = new List<NizimaNamedOption>();
        foreach (var item in array.EnumerateArray())
        {
            var type = item.TryGetProperty("ItemType", out var typeNode) ? typeNode.GetString() : null;
            if (!string.Equals(type, "Live2D", StringComparison.OrdinalIgnoreCase))
                continue;
            var id = item.TryGetProperty("ItemId", out var idNode) ? idNode.GetString() : null;
            if (string.IsNullOrWhiteSpace(id))
                continue;
            var name = item.TryGetProperty("Name", out var nameNode) ? nameNode.GetString() ?? "" : "";
            list.Add(new NizimaNamedOption(name, id));
        }

        return list;
    }
}
