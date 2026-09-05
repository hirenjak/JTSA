using JTSA.Dao;
using JTSA.Forms;
using System.Text.Json;

namespace JTSA.Utility;

internal sealed record ParticipationSnapshot(List<ParticipationUserForm> Users, List<string> RedemptionIds)
{
    public List<ParticipationUserForm> PlayingUsers { get; init; } = [];
    public int SlotCount { get; init; }
    public bool ObsVisible { get; init; }
    public Dictionary<string, int> ParticipationCounts { get; init; } = new();
}

internal static class ParticipationStore
{
    internal static void Clear(string accountId)
    {
        if (string.IsNullOrWhiteSpace(accountId)) return;
        var snapshots = Read();
        var previous = snapshots.GetValueOrDefault(accountId) ?? new([], []);
        // Keep redemption deduplication and capacity, but reset even users already removed from the lists.
        snapshots[accountId] = new([], previous.RedemptionIds) { SlotCount = previous.SlotCount, ObsVisible = previous.ObsVisible };
        if (!DAO_Setting.InsertUpdate(DAO_Setting.SettingName.ParticipationLists, JsonSerializer.Serialize(snapshots)))
            throw new InvalidOperationException("参加者一覧と参加回数をリセットできませんでした。");
    }

    private static Dictionary<string, ParticipationSnapshot> Read()
    {
        var json = DAO_Setting.SelectOneById(DAO_Setting.SettingName.ParticipationLists)?.Value;
        return string.IsNullOrWhiteSpace(json) ? new() :
            JsonSerializer.Deserialize<Dictionary<string, ParticipationSnapshot>>(json) ?? new();
    }

    internal static ParticipationSnapshot Load(string accountId) =>
        Read().GetValueOrDefault(accountId) ?? new([], []);

    internal static int GetParticipationCount(string accountId, string userId)
    {
        var saved = Load(accountId);
        return saved.Users.Concat(saved.PlayingUsers).FirstOrDefault(x => x.UserId == userId)?.ParticipationCount
            ?? saved.ParticipationCounts.GetValueOrDefault(userId);
    }

    internal static void Save(string accountId, IEnumerable<ParticipationUserForm> users, IEnumerable<string> redemptionIds,
        IEnumerable<ParticipationUserForm>? playingUsers = null, int? slotCount = null, bool? obsVisible = null)
    {
        if (string.IsNullOrWhiteSpace(accountId)) return;
        var snapshots = Read();
        var previous = snapshots.GetValueOrDefault(accountId) ?? new([], []);
        var counts = new Dictionary<string, int>(previous.ParticipationCounts);
        // Import older snapshots too, before a removal or clear discards their list entries.
        foreach (var user in previous.Users.Concat(previous.PlayingUsers))
            counts[user.UserId] = user.ParticipationCount;
        var waiting = users.ToList();
        var playing = playingUsers?.ToList() ?? [];
        foreach (var user in waiting.Concat(playing))
            counts[user.UserId] = user.ParticipationCount;
        snapshots[accountId] = new(waiting, redemptionIds.TakeLast(5000).ToList())
        { PlayingUsers = playing, ParticipationCounts = counts, SlotCount = slotCount ?? previous.SlotCount,
          ObsVisible = obsVisible ?? previous.ObsVisible };
        if (!DAO_Setting.InsertUpdate(DAO_Setting.SettingName.ParticipationLists, JsonSerializer.Serialize(snapshots)))
            throw new InvalidOperationException("参加者一覧を保存できませんでした。");
    }
}
