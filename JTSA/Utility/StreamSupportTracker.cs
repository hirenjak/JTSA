using JTSA.Forms;

namespace JTSA.Utility
{
    /// <summary>
    /// 現在の配信中に受け取った支援イベントを集計する。
    /// </summary>
    public static class StreamSupportTracker
    {
        private static readonly object syncRoot = new();
        private static readonly Dictionary<string, BitsUserForm> bitsUsers = new(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, SubscribeUserForm> subscribeUsers = new(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, RaidedUserForm> raidedUsers = new(StringComparer.OrdinalIgnoreCase);

        public static event Action? Changed;

        public static IReadOnlyList<BitsUserForm> BitsUsers
        {
            get { lock (syncRoot) return bitsUsers.Values.OrderByDescending(x => x.BitsAmount).ToList(); }
        }

        public static IReadOnlyList<SubscribeUserForm> SubscribeUsers
        {
            get { lock (syncRoot) return subscribeUsers.Values.OrderByDescending(x => x.SubscribeAmount).ToList(); }
        }

        public static IReadOnlyList<RaidedUserForm> RaidedUsers
        {
            get { lock (syncRoot) return raidedUsers.Values.OrderByDescending(x => x.ViewerCount).ToList(); }
        }

        public static void Reset()
        {
            lock (syncRoot)
            {
                bitsUsers.Clear();
                subscribeUsers.Clear();
                raidedUsers.Clear();
            }
            Changed?.Invoke();
        }

        public static void AddBits(string userName, int amount)
        {
            if (string.IsNullOrWhiteSpace(userName) || amount <= 0) return;
            lock (syncRoot)
            {
                if (!bitsUsers.TryGetValue(userName, out var user))
                    bitsUsers[userName] = user = new BitsUserForm { UserName = userName };
                user.BitsAmount += amount;
            }
            Changed?.Invoke();
        }

        public static void AddSubscription(string userName, int amount = 1)
        {
            if (string.IsNullOrWhiteSpace(userName) || amount <= 0) return;
            lock (syncRoot)
            {
                if (!subscribeUsers.TryGetValue(userName, out var user))
                    subscribeUsers[userName] = user = new SubscribeUserForm { UserName = userName };
                user.SubscribeAmount += amount;
            }
            Changed?.Invoke();
        }

        public static void AddRaid(string userName, int viewerCount)
        {
            if (string.IsNullOrWhiteSpace(userName)) return;
            lock (syncRoot)
            {
                if (!raidedUsers.TryGetValue(userName, out var user))
                    raidedUsers[userName] = user = new RaidedUserForm { UserName = userName };
                user.ViewerCount += Math.Max(0, viewerCount);
            }
            Changed?.Invoke();
        }
    }
}
