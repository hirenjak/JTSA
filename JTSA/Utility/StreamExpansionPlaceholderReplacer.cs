namespace JTSA.Utility;

public sealed record RaidPlaceholderValues(string UserName, string Title, string Category);
public sealed record ChatPlaceholderValues(string UserName, string UserLogin);
public sealed record StreamExpansionTriggerValues(
    string TriggerType,
    string TriggerValue,
    string TriggerObs,
    string StreamTitle,
    string StreamCategory,
    string ChannelPointInput = "",
    string StreamCategoryJapanese = "",
    string StreamBitsUsers = "",
    string StreamSubscribeUsers = "",
    string StreamRaidUsers = "",
    string StreamFollowUsers = "");

public static class StreamExpansionPlaceholderReplacer
{
    public const string CurrentTimePlaceholder = "{current_time}";
    public const string RaidUserPlaceholder = "{raid_user}";
    public const string RaidTitlePlaceholder = "{raid_title}";
    public const string RaidCategoryPlaceholder = "{raid_category}";
    public const string ChatUserPlaceholder = "{chat_user}";
    public const string ChatLoginPlaceholder = "{chat_login}";
    public const string TriggerTypePlaceholder = "{trigger_type}";
    public const string TriggerValuePlaceholder = "{trigger_value}";
    public const string TriggerObsPlaceholder = "{trigger_obs}";
    public const string StreamTitlePlaceholder = "{stream_title}";
    public const string StreamCategoryPlaceholder = "{stream_category}";
    public const string StreamCategoryJapanesePlaceholder = "{stream_category_ja}";
    public const string StreamBitsUsersPlaceholder = "{stream_bits_users}";
    public const string StreamSubscribeUsersPlaceholder = "{stream_subscribe_users}";
    public const string StreamRaidUsersPlaceholder = "{stream_raid_users}";
    public const string StreamFollowUsersPlaceholder = "{stream_follow_users}";
    public const string ChannelPointInputPlaceholder = "{channel_point_input}";

    public static string Replace(
        string content,
        RaidPlaceholderValues? raid,
        ChatPlaceholderValues? chat = null,
        StreamExpansionTriggerValues? trigger = null)
    {
        // Use the PC's local time when the action executes (after any configured delay).
        var currentTime = DateTime.Now.ToString("HH:mm", System.Globalization.CultureInfo.InvariantCulture);
        content = content
            .Replace(CurrentTimePlaceholder, currentTime, StringComparison.OrdinalIgnoreCase)
            .Replace(RaidUserPlaceholder, raid?.UserName ?? string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace(RaidTitlePlaceholder, raid?.Title ?? string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace(RaidCategoryPlaceholder, raid?.Category ?? string.Empty, StringComparison.OrdinalIgnoreCase);

        content = content
            .Replace(ChatUserPlaceholder, chat?.UserName ?? string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace(ChatLoginPlaceholder, chat?.UserLogin ?? string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace(TriggerTypePlaceholder, trigger?.TriggerType ?? string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace(TriggerValuePlaceholder, trigger?.TriggerValue ?? string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace(TriggerObsPlaceholder, trigger?.TriggerObs ?? string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace(StreamTitlePlaceholder, trigger?.StreamTitle ?? string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace(StreamCategoryPlaceholder, trigger?.StreamCategory ?? string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace(StreamCategoryJapanesePlaceholder, trigger?.StreamCategoryJapanese ?? string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace(StreamBitsUsersPlaceholder, trigger?.StreamBitsUsers ?? string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace(StreamSubscribeUsersPlaceholder, trigger?.StreamSubscribeUsers ?? string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace(StreamRaidUsersPlaceholder, trigger?.StreamRaidUsers ?? string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace(StreamFollowUsersPlaceholder, trigger?.StreamFollowUsers ?? string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace(ChannelPointInputPlaceholder, trigger?.ChannelPointInput ?? string.Empty, StringComparison.OrdinalIgnoreCase);

        return content;
    }
}
