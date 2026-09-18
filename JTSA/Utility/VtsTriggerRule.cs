using System.Text.Json;
using JTSA.Dao;

namespace JTSA.Utility;

internal static class VtsTriggerTypes
{
    public const string ChannelPoint = nameof(StreamExpansionTriggerType.ChannelPoint);
    public const string Chat = nameof(StreamExpansionTriggerType.Chat);
    public const string FirstChat = nameof(StreamExpansionTriggerType.FirstChat);
    public const string Follow = nameof(StreamExpansionTriggerType.Follow);
    public const string Raid = nameof(StreamExpansionTriggerType.Raid);
    public const string Subscribe = nameof(StreamExpansionTriggerType.Subscribe);
    public const string Bits = nameof(StreamExpansionTriggerType.Bits);
    public const string Hourly = nameof(StreamExpansionTriggerType.Hourly);
    public const string ScheduledTime = nameof(StreamExpansionTriggerType.ScheduledTime);
    public const string AdStart = nameof(StreamExpansionTriggerType.AdStart);
    public const string AdEnd = nameof(StreamExpansionTriggerType.AdEnd);
    public const string AdUpcoming = nameof(StreamExpansionTriggerType.AdUpcoming);
    public const string ObsStreamStart = nameof(StreamExpansionTriggerType.ObsStreamStart);
}

internal static class VtsTriggerCommands
{
    public const string LoadModel = "LoadModel";
    public const string TriggerHotkey = "TriggerHotkey";
    public const string ExpressionOn = "ExpressionOn";
    public const string ExpressionOff = "ExpressionOff";
    public const string LoadItem = "LoadItem";
    public const string UnloadItem = "UnloadItem";
    public const string MoveModel = "MoveModel";
    public const string Tint = "Tint";
    public const string PostProcessing = "PostProcessing";
    public const string RawJson = "RawJson";
}

internal sealed class VtsTriggerCommandExtra
{
    public double Time { get; set; } = 0.5;
    public double X { get; set; }
    public double Y { get; set; }
    public double Rotation { get; set; }
    public double Size { get; set; }
    public bool Relative { get; set; }
    public int R { get; set; } = 255;
    public int G { get; set; } = 255;
    public int B { get; set; } = 255;
    public int A { get; set; } = 255;
    public bool TintAll { get; set; } = true;
    public bool PostProcessingOn { get; set; } = true;
    public double PostProcessingValue { get; set; } = 1;
    public string RawJson { get; set; } = "";
}

internal sealed class VtsTriggerRule
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public bool IsEnabled { get; set; } = true;
    public string TriggerType { get; set; } = VtsTriggerTypes.ChannelPoint;
    public string TriggerValue { get; set; } = "";
    public string CommandType { get; set; } = VtsTriggerCommands.TriggerHotkey;
    public string CommandValue { get; set; } = "";
    public VtsTriggerCommandExtra Extra { get; set; } = new();
}

internal static class VtsTriggerStore
{
    public static List<VtsTriggerRule> Load()
    {
        var json = DAO_Setting.SelectOneById(DAO_Setting.SettingName.VtsTriggerRules)?.Value;
        if (string.IsNullOrWhiteSpace(json))
            return [];

        try
        {
            return JsonSerializer.Deserialize<List<VtsTriggerRule>>(json) ?? [];
        }
        catch
        {
            return [];
        }
    }

    public static void Save(IEnumerable<VtsTriggerRule> rules)
    {
        DAO_Setting.InsertUpdate(
            DAO_Setting.SettingName.VtsTriggerRules,
            JsonSerializer.Serialize(rules.ToList()));
    }
}

internal static class VtsTriggerMatcher
{
    public static bool Matches(VtsTriggerRule rule, StreamExpansionTriggerType type, string value)
    {
        if (!rule.IsEnabled)
            return false;

        if (!string.Equals(rule.TriggerType, type.ToString(), StringComparison.Ordinal))
            return false;

        return type switch
        {
            StreamExpansionTriggerType.ChannelPoint =>
                !string.IsNullOrWhiteSpace(rule.TriggerValue) &&
                string.Equals(value, rule.TriggerValue, StringComparison.OrdinalIgnoreCase),
            StreamExpansionTriggerType.Chat =>
                !string.IsNullOrWhiteSpace(rule.TriggerValue) &&
                value.Contains(rule.TriggerValue, StringComparison.OrdinalIgnoreCase),
            StreamExpansionTriggerType.ScheduledTime =>
                string.Equals(value, rule.TriggerValue, StringComparison.Ordinal),
            StreamExpansionTriggerType.AdUpcoming =>
                string.Equals(value, rule.TriggerValue, StringComparison.Ordinal),
            StreamExpansionTriggerType.ObsStreamStart =>
                string.Equals(value, rule.TriggerValue, StringComparison.OrdinalIgnoreCase),
            _ => true
        };
    }
}
