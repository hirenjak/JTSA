namespace JTSA.Utility;

public sealed record RaidPlaceholderValues(string UserName, string Title, string Category);

public static class StreamExpansionPlaceholderReplacer
{
    public const string RaidUserPlaceholder = "{raid_user}";
    public const string RaidTitlePlaceholder = "{raid_title}";
    public const string RaidCategoryPlaceholder = "{raid_category}";

    public static string Replace(string content, RaidPlaceholderValues? raid)
    {
        if (raid is null) return content;

        return content
            .Replace(RaidUserPlaceholder, raid.UserName ?? string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace(RaidTitlePlaceholder, raid.Title ?? string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace(RaidCategoryPlaceholder, raid.Category ?? string.Empty, StringComparison.OrdinalIgnoreCase);
    }
}
