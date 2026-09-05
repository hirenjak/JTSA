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
        private static readonly Dictionary<string, FollowUserForm> followUsers = new(StringComparer.OrdinalIgnoreCase);
        private static string activeStreamId = string.Empty;

        private sealed class SupportSnapshot
        {
            public string StreamId { get; set; } = string.Empty;
            public List<BitsUserForm> BitsUsers { get; set; } = [];
            public List<SubscribeUserForm> SubscribeUsers { get; set; } = [];
            public List<RaidedUserForm> RaidedUsers { get; set; } = [];
            public List<FollowUserForm> FollowUsers { get; set; } = [];
        }

        private sealed class SupportArchive
        {
            public Dictionary<string, SupportSnapshot> Streams { get; set; } = new();
        }

        public static event Action? Changed;

        public static IReadOnlyList<BitsUserForm> BitsUsers
        {
            get { lock (syncRoot) return bitsUsers.Values.OrderByDescending(x => x.BitsAmount).ToList(); }
        }

        public static IReadOnlyList<SubscribeUserForm> SubscribeUsers
        {
            get
            {
                lock (syncRoot)
                    return subscribeUsers.Values
                        .OrderByDescending(x => x.IsGift ? x.GiftCount : x.CumulativeMonths)
                        .ThenBy(x => x.UserName)
                        .ToList();
            }
        }

        public static IReadOnlyList<RaidedUserForm> RaidedUsers
        {
            get { lock (syncRoot) return raidedUsers.Values.OrderByDescending(x => x.ViewerCount).ToList(); }
        }

        public static IReadOnlyList<FollowUserForm> FollowUsers
        {
            get { lock (syncRoot) return followUsers.Values.OrderByDescending(x => x.FollowedAt).ToList(); }
        }

        internal static string FormatBitsUsers() => string.Join(
            Environment.NewLine,
            BitsUsers.Select(user => $"{user.UserName}: {user.BitsAmount:N0} Bits"));

        internal static string FormatSubscribeUsers() => string.Join(
            Environment.NewLine,
            SubscribeUsers.Select(user => $"{user.UserName}: {user.DetailText}"));

        internal static string FormatRaidUsers() => string.Join(
            Environment.NewLine,
            RaidedUsers.Select(user => $"{user.UserName}: {user.ViewerCount:N0}人"));

        internal static string FormatFollowUsers() => string.Join(
            Environment.NewLine,
            FollowUsers.Select(user => user.UserName));

        public static void Reset()
        {
            lock (syncRoot)
            {
                activeStreamId = string.Empty;
                bitsUsers.Clear();
                subscribeUsers.Clear();
                raidedUsers.Clear();
                followUsers.Clear();
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
                followUsers.Clear();

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

        public static void AddSubscription(string userName, int cumulativeMonths = 1, string tier = "1")
        {
            if (string.IsNullOrWhiteSpace(userName) || cumulativeMonths <= 0) return;
            lock (syncRoot)
            {
                var key = $"subscription:{userName}";
                if (!subscribeUsers.TryGetValue(key, out var user))
                    subscribeUsers[key] = user = new SubscribeUserForm { UserName = userName };
                user.IsGift = false;
                user.Tier = NormalizeTier(tier);
                user.CumulativeMonths = Math.Max(user.CumulativeMonths, cumulativeMonths);
                SaveSnapshot();
            }
            Changed?.Invoke();
        }

        public static void AddGiftSubscription(string userName, string tier, int amount = 1)
        {
            if (string.IsNullOrWhiteSpace(userName) || amount <= 0) return;
            tier = NormalizeTier(tier);
            lock (syncRoot)
            {
                var key = $"gift:{userName}:{tier}";
                if (!subscribeUsers.TryGetValue(key, out var user))
                    subscribeUsers[key] = user = new SubscribeUserForm
                    {
                        UserName = userName,
                        IsGift = true,
                        Tier = tier
                    };
                user.GiftCount += amount;
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

        public static void AddFollow(string userName)
        {
            if (string.IsNullOrWhiteSpace(userName)) return;
            lock (syncRoot)
            {
                followUsers[userName] = new FollowUserForm
                {
                    UserName = userName,
                    FollowedAt = DateTime.Now
                };
                SaveSnapshot();
            }
            Changed?.Invoke();
        }

        public static void RemoveUsers(
            string? bitsUserName = null,
            string? subscribeUserName = null,
            string? raidUserName = null,
            string? followUserName = null)
        {
            lock (syncRoot)
            {
                if (!string.IsNullOrWhiteSpace(bitsUserName)) bitsUsers.Remove(bitsUserName);
                if (!string.IsNullOrWhiteSpace(subscribeUserName))
                {
                    foreach (var key in subscribeUsers
                                 .Where(x => string.Equals(x.Value.UserName, subscribeUserName, StringComparison.OrdinalIgnoreCase))
                                 .Select(x => x.Key)
                                 .ToList())
                        subscribeUsers.Remove(key);
                }
                if (!string.IsNullOrWhiteSpace(raidUserName)) raidedUsers.Remove(raidUserName);
                if (!string.IsNullOrWhiteSpace(followUserName)) followUsers.Remove(followUserName);
                SaveSnapshot();
            }
            Changed?.Invoke();
        }

        public static void RemoveUsersByPrefixes(params string[] userNamePrefixes)
        {
            userNamePrefixes = userNamePrefixes.Where(x => !string.IsNullOrWhiteSpace(x)).ToArray();
            if (userNamePrefixes.Length == 0) return;
            lock (syncRoot)
            {
                RemoveMatchingUsers(bitsUsers, userNamePrefixes);
                RemoveMatchingUsers(subscribeUsers, userNamePrefixes);
                RemoveMatchingUsers(raidedUsers, userNamePrefixes);
                RemoveMatchingUsers(followUsers, userNamePrefixes);
                SaveSnapshot();
            }
            Changed?.Invoke();
        }

        private static void RemoveMatchingUsers<T>(Dictionary<string, T> users, IReadOnlyCollection<string> prefixes)
        {
            foreach (var key in users
                         .Where(x => prefixes.Any(prefix =>
                             GetUserName(x.Value).StartsWith(prefix, StringComparison.OrdinalIgnoreCase)))
                         .Select(x => x.Key)
                         .ToList())
                users.Remove(key);
        }

        private static string GetUserName<T>(T user) => user switch
        {
            BitsUserForm bits => bits.UserName,
            SubscribeUserForm subscribe => subscribe.UserName,
            RaidedUserForm raid => raid.UserName,
            FollowUserForm follow => follow.UserName,
            _ => string.Empty
        };

        private static void RestoreSnapshot(string streamId)
        {
            try
            {
                var archive = ReadArchive();
                if (!archive.Streams.TryGetValue(streamId, out var snapshot)) return;

                foreach (var user in snapshot.BitsUsers)
                    bitsUsers[user.UserName] = user;
                foreach (var user in snapshot.SubscribeUsers)
                {
                    var key = user.IsGift
                        ? $"gift:{user.UserName}:{NormalizeTier(user.Tier)}"
                        : $"subscription:{user.UserName}";
                    subscribeUsers[key] = user;
                }
                foreach (var user in snapshot.RaidedUsers)
                    raidedUsers[user.UserName] = user;
                foreach (var user in snapshot.FollowUsers)
                    followUsers[user.UserName] = user;
            }
            catch (Exception)
            {
                // 保存値の破損や一時的なDBエラーでは、現在の配信を空の集計から開始する。
            }
        }

        private static SupportArchive ReadArchive()
        {
            var json = DAO_Setting.SelectOneById(DAO_Setting.SettingName.StreamSupportSnapshot)?.Value;
            if (string.IsNullOrWhiteSpace(json)) return new SupportArchive();

            try
            {
                using var document = JsonDocument.Parse(json);
                if (document.RootElement.ValueKind != JsonValueKind.Object) return new SupportArchive();
                if (document.RootElement.TryGetProperty(nameof(SupportArchive.Streams), out _))
                {
                    var saved = JsonSerializer.Deserialize<SupportArchive>(json);
                    return saved?.Streams is not null ? saved : new SupportArchive();
                }

                // 旧バージョンの1配信分の保存値も引き継ぐ。
                var legacy = JsonSerializer.Deserialize<SupportSnapshot>(json);
                var archive = new SupportArchive();
                if (legacy is not null && !string.IsNullOrWhiteSpace(legacy.StreamId))
                    archive.Streams[legacy.StreamId] = legacy;
                return archive;
            }
            catch (JsonException)
            {
                // 壊れた保存値があっても、新しく受信した集計は保存できるようにする。
                return new SupportArchive();
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
                RaidedUsers = raidedUsers.Values.ToList(),
                FollowUsers = followUsers.Values.ToList()
            };
            try
            {
                var archive = ReadArchive();
                archive.Streams[activeStreamId] = snapshot;
                DAO_Setting.InsertUpdate(
                    DAO_Setting.SettingName.StreamSupportSnapshot,
                    JsonSerializer.Serialize(archive));
            }
            catch (Exception)
            {
                // 保存失敗でチャット・EventSubの受信処理を止めない。
            }
        }

        private static string NormalizeTier(string? tier) => tier?.Trim() switch
        {
            "1000" or "1" => "1",
            "2000" or "2" => "2",
            "3000" or "3" => "3",
            "Prime" or "prime" => "Prime",
            { Length: > 0 } value => value,
            _ => "1"
        };
    }
}
