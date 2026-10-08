using JTSA.Models;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace JTSA.Dao
{
    class DAO_StreamChatUserCount
    {
        public static void Increment(
            DateTime chatDateTime,
            string userId,
            string loginId,
            string displayName,
            string streamId = "")
        {
            IncrementBatch([new ChatCountEntry(chatDateTime, userId, loginId, displayName, streamId)]);
        }

        internal sealed record ChatCountEntry(DateTime Time, string UserId, string LoginId, string DisplayName, string StreamId);

        internal static void IncrementBatch(IReadOnlyList<ChatCountEntry> entries)
        {
            var groups = entries.Where(x => !string.IsNullOrWhiteSpace(x.UserId))
                .GroupBy(x => (StreamId: string.IsNullOrWhiteSpace(x.StreamId) ? $"untracked-{x.Time:yyyyMMdd}" : x.StreamId, x.UserId))
                .ToList();
            if (groups.Count == 0) return;
            var now = DateTime.Now;
            const int maxAttempts = 4;

            for (var attempt = 1; ; attempt++)
            {
                try
                {
                    using var db = new AppDbContext();
                    using var transaction = db.Database.BeginTransaction();
                    foreach (var group in groups)
                    {
                        var latest = group.Last();
                        var first = group.Min(x => x.Time);
                        var last = group.Max(x => x.Time);
                        db.Database.ExecuteSqlInterpolated($"""
                            INSERT INTO "T_StreamChatUserCount"
                                ("StreamId", "UserId", "LoginId", "DisplayName", "ChatCount", "FirstChatDateTime", "LastChatDateTime", "CreatedDateTime", "UpdatedDateTime")
                            VALUES
                                ({group.Key.StreamId}, {group.Key.UserId}, {latest.LoginId}, {latest.DisplayName}, {group.Count()}, {first}, {last}, {now}, {now})
                            ON CONFLICT ("StreamId", "UserId") DO UPDATE SET
                                "LoginId" = excluded."LoginId",
                                "DisplayName" = excluded."DisplayName",
                                "ChatCount" = "T_StreamChatUserCount"."ChatCount" + excluded."ChatCount",
                                "FirstChatDateTime" = MIN("T_StreamChatUserCount"."FirstChatDateTime", excluded."FirstChatDateTime"),
                                "LastChatDateTime" = MAX("T_StreamChatUserCount"."LastChatDateTime", excluded."LastChatDateTime"),
                                "UpdatedDateTime" = excluded."UpdatedDateTime";
                            """);
                    }
                    transaction.Commit();
                    return;
                }
                catch (SqliteException ex) when (ex.SqliteErrorCode is 5 or 6 && attempt < maxAttempts)
                {
                    Thread.Sleep(50 * attempt);
                }
            }
        }

        public static List<T_StreamChatUserCount> SelectAll()
        {
            using var db = new AppDbContext();
            return db.T_StreamChatUserCount.AsNoTracking()
                .OrderByDescending(x => x.LastChatDateTime).ToList();
        }

        public static List<T_StreamChatUserCount> SelectByUserId(string userId)
        {
            using var db = new AppDbContext();
            return db.T_StreamChatUserCount.AsNoTracking()
                .Where(x => x.UserId == userId)
                .OrderBy(x => x.FirstChatDateTime).ToList();
        }

        public static bool HasAny()
        {
            using var db = new AppDbContext();
            return db.T_StreamChatUserCount.Any();
        }

        // 配信履歴があれば開始日、なければ初回発言日を集計日とする。
        public static List<T_StreamChatUserCount> SelectByPeriod(DateTime? startDate, DateTime? endDate)
        {
            using var db = new AppDbContext();
            var query = from count in db.T_StreamChatUserCount.AsNoTracking()
                        join stream in db.T_StreamHistory on count.StreamId equals stream.StreamId into streams
                        from stream in streams.DefaultIfEmpty()
                        select new { Count = count, Date = stream == null ? count.FirstChatDateTime : stream.StartedAt };
            if (startDate.HasValue)
            {
                var start = startDate.Value.Date;
                query = query.Where(x => x.Date >= start);
            }
            if (endDate.HasValue && endDate.Value.Date < DateTime.MaxValue.Date)
            {
                var endExclusive = endDate.Value.Date.AddDays(1);
                query = query.Where(x => x.Date < endExclusive);
            }
            return query.OrderByDescending(x => x.Count.LastChatDateTime).Select(x => x.Count).ToList();
        }

        public static List<T_StreamChatUserCount> SelectByStreamId(string streamId)
        {
            using var db = new AppDbContext();
            return db.T_StreamChatUserCount.AsNoTracking()
                .Where(x => x.StreamId == streamId)
                .OrderByDescending(x => x.ChatCount)
                .ThenBy(x => x.DisplayName).ToList();
        }
    }
}
