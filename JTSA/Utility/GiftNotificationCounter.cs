namespace JTSA.Utility;

/// <summary>同一originのまとめ通知と個別通知を、到着順に依存せず合算する。</summary>
internal sealed class GiftNotificationCounter
{
    private const int Capacity = 10000;
    private readonly object sync = new();
    private readonly HashSet<string> messageIds = new(StringComparer.Ordinal);
    private readonly Queue<string> messageOrder = new();
    private readonly Dictionary<string, (int Individuals, int Total)> groups = new(StringComparer.Ordinal);
    private readonly Queue<string> groupOrder = new();

    public int CountNew(string? messageId, string? originId, bool isCommunity, int amount)
    {
        if (amount <= 0) return 0;
        lock (sync)
        {
            if (!string.IsNullOrWhiteSpace(messageId))
            {
                if (!messageIds.Add(messageId)) return 0;
                messageOrder.Enqueue(messageId);
                if (messageOrder.Count > Capacity) messageIds.Remove(messageOrder.Dequeue());
            }

            // 関連IDがなければ、名前・時刻だけで別の贈り物を同一視しない。
            if (string.IsNullOrWhiteSpace(originId)) return amount;
            if (!groups.TryGetValue(originId, out var group))
            {
                groupOrder.Enqueue(originId);
                if (groupOrder.Count > Capacity) groups.Remove(groupOrder.Dequeue());
            }
            var before = Math.Max(group.Individuals, group.Total);
            if (isCommunity) group.Total = Math.Max(group.Total, amount);
            else group.Individuals += amount;
            groups[originId] = group;
            return Math.Max(group.Individuals, group.Total) - before;
        }
    }
}
