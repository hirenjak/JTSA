using System.Collections.ObjectModel;
using JTSA.Plugin.Abstractions;

namespace JTSA.NizimaLivePlugin;

internal static class NizimaChannelPointCatalog
{
    private static readonly StringComparer IdComparer = StringComparer.OrdinalIgnoreCase;

    public static void ReplaceKeeping(
        ObservableCollection<ChannelPointRewardInfo> target,
        IEnumerable<ChannelPointRewardInfo>? items,
        IEnumerable<string> keepIds)
    {
        var next = (items ?? [])
            .Where(item => !string.IsNullOrWhiteSpace(item.Id))
            .GroupBy(item => item.Id, IdComparer)
            .Select(group => group.First())
            .ToList();
        var keep = keepIds
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .ToHashSet(IdComparer);

        foreach (var id in keep)
        {
            if (next.Any(item => IdComparer.Equals(item.Id, id)))
                continue;
            var existing = target.FirstOrDefault(item => IdComparer.Equals(item.Id, id));
            next.Add(existing ?? new ChannelPointRewardInfo(id, id, false));
        }

        for (var index = target.Count - 1; index >= 0; index--)
        {
            if (next.All(item => !IdComparer.Equals(item.Id, target[index].Id)))
                target.RemoveAt(index);
        }

        foreach (var item in next)
        {
            var currentIndex = IndexOfId(target, item.Id);
            if (currentIndex < 0)
                target.Add(item);
            else if (!Equals(target[currentIndex], item))
                target[currentIndex] = item;
        }
    }

    public static void EnsureOption(ObservableCollection<ChannelPointRewardInfo> list, string id)
    {
        if (string.IsNullOrWhiteSpace(id) || IndexOfId(list, id) >= 0)
            return;
        list.Add(new ChannelPointRewardInfo(id, id, false));
    }

    public static string TitleOf(IReadOnlyList<ChannelPointRewardInfo>? rewards, string id)
    {
        if (string.IsNullOrWhiteSpace(id))
            return "";
        var title = rewards?.FirstOrDefault(item => IdComparer.Equals(item.Id, id))?.Title;
        return string.IsNullOrWhiteSpace(title) ? id : title;
    }

    private static int IndexOfId(ObservableCollection<ChannelPointRewardInfo> list, string id)
    {
        for (var index = 0; index < list.Count; index++)
        {
            if (IdComparer.Equals(list[index].Id, id))
                return index;
        }

        return -1;
    }
}
