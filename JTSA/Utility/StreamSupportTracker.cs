using JTSA.Forms;
using JTSA.Dao;
using System.Text.Json;

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
        private static string activeStreamId = string.Empty;

        private sealed class SupportSnapshot
        {
            public string StreamId { get; set; } = string.Empty;
            public List<BitsUserForm> BitsUsers { get; set; } = [];
            public List<SubscribeUserForm> SubscribeUsers { get; set; } = [];
            public List<RaidedUserForm> RaidedUsers { get; set; } = [];
        }

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
                activeStreamId = string.Empty;
                bitsUsers.Clear();
                subscribeUsers.Clear();
                raidedUsers.Clear();
            }
            Changed?.Invoke();
        }

        /// <summary>配信IDを切り替え、同じ配信の保存済み集計があれば復元する。</summary>
        public static void StartStream(string? streamId)
        {
            streamId ??= string.Empty;
            lock (syncRoot)
            {
                if (activeStreamId == streamId) return;

                activeStreamId = streamId;
                bitsUsers.Clear();
                subscribeUsers.Clear();
                raidedUsers.Clear();

                if (!string.IsNullOrWhiteSpace(streamId))
                {
                    RestoreSnapshot(streamId);
                }
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
                SaveSnapshot();
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
                SaveSnapshot();
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
                SaveSnapshot();
            }
            Changed?.Invoke();
        }

        private static void RestoreSnapshot(string streamId)
        {
            try
            {
                var json = DAO_Setting.SelectOneById(DAO_Setting.SettingName.StreamSupportSnapshot)?.Value;
                if (string.IsNullOrWhiteSpace(json)) return;

                var snapshot = JsonSerializer.Deserialize<SupportSnapshot>(json);
                if (snapshot?.StreamId != streamId) return;

                foreach (var user in snapshot.BitsUsers)
                    bitsUsers[user.UserName] = user;
                foreach (var user in snapshot.SubscribeUsers)
                    subscribeUsers[user.UserName] = user;
                foreach (var user in snapshot.RaidedUsers)
                    raidedUsers[user.UserName] = user;
            }
            catch (Exception)
            {
                // 保存値の破損や一時的なDBエラーでは、現在の配信を空の集計から開始する。
            }
        }

        private static void SaveSnapshot()
        {
            if (string.IsNullOrWhiteSpace(activeStreamId)) return;

            var snapshot = new SupportSnapshot
            {
                StreamId = activeStreamId,
                BitsUsers = bitsUsers.Values.ToList(),
                SubscribeUsers = subscribeUsers.Values.ToList(),
                RaidedUsers = raidedUsers.Values.ToList()
            };
            try
            {
                DAO_Setting.InsertUpdate(
                    DAO_Setting.SettingName.StreamSupportSnapshot,
                    JsonSerializer.Serialize(snapshot));
            }
            catch (Exception)
            {
                // 保存失敗でチャット・EventSubの受信処理を止めない。
            }
        }
    }
}
