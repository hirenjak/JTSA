using System.Collections.Concurrent;

namespace JTSA.Utility;

/// <summary>
/// 配信ごとに、ユーザーの最初のチャット入室を判定する。
/// </summary>
internal sealed class StreamChatEntranceTracker
{
    private readonly ConcurrentDictionary<string, byte> enteredUsers = new();

    public bool TryEnter(string? streamId, string? userId)
    {
        if (string.IsNullOrWhiteSpace(streamId) || string.IsNullOrWhiteSpace(userId))
        {
            return false;
        }

        return enteredUsers.TryAdd($"{streamId}\0{userId}", 0);
    }

    /// <summary>永続化済みの入室履歴を、発火させずに復元する。</summary>
    public void Restore(string? streamId, IEnumerable<string?> userIds)
    {
        if (string.IsNullOrWhiteSpace(streamId))
        {
            return;
        }

        foreach (var userId in userIds)
        {
            if (!string.IsNullOrWhiteSpace(userId))
            {
                enteredUsers.TryAdd($"{streamId}\0{userId}", 0);
            }
        }
    }

    public void Clear() => enteredUsers.Clear();
}
