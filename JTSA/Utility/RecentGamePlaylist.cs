namespace JTSA.Utility;

internal static class RecentGamePlaylist
{
    internal static IReadOnlyList<string> SelectCategoryIds(string currentId, IEnumerable<string> history)
    {
        var recent = history.Where(x => !string.IsNullOrWhiteSpace(x) && x != currentId)
            .Distinct(StringComparer.Ordinal).Take(5);
        return (string.IsNullOrWhiteSpace(currentId) ? recent : recent.Prepend(currentId)).ToList();
    }
}
