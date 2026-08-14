namespace JTSA.Utility;

public sealed record RaidPlaceholderValues(string UserName, string Title, string Category);
public sealed record ChatPlaceholderValues(string UserName, string UserLogin);

public static class StreamExpansionPlaceholderReplacer
{
    public const string RaidUserPlaceholder = "{raid_user}";
    public const string RaidTitlePlaceholder = "{raid_title}";
    public const string RaidCategoryPlaceholder = "{raid_category}";
    public const string ChatUserPlaceholder = "{chat_user}";
    public const string ChatLoginPlaceholder = "{chat_login}";

    public static string Replace(
        string content,
        RaidPlaceholderValues? raid,
        ChatPlaceholderValues? chat = null)
    {
        if (raid is not null)
        {
            content = content
                .Replace(RaidUserPlaceholder, raid.UserName ?? string.Empty, StringComparison.OrdinalIgnoreCase)
                .Replace(RaidTitlePlaceholder, raid.Title ?? string.Empty, StringComparison.OrdinalIgnoreCase)
                .Replace(RaidCategoryPlaceholder, raid.Category ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        }

        if (chat is not null)
        {
            content = content
                .Replace(ChatUserPlaceholder, chat.UserName ?? string.Empty, StringComparison.OrdinalIgnoreCase)
                .Replace(ChatLoginPlaceholder, chat.UserLogin ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        }

        return content;
    }
}
