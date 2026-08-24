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
            if (string.IsNullOrWhiteSpace(userId)) return;

            var resolvedStreamId = string.IsNullOrWhiteSpace(streamId)
                ? $"untracked-{chatDateTime:yyyyMMdd}"
                : streamId;
            var now = DateTime.Now;
            const int maxAttempts = 4;

            for (var attempt = 1; ; attempt++)
            {
                try
                {
                    using var db = new AppDbContext();
                    db.Database.ExecuteSqlInterpolated($"""
                        INSERT INTO "T_StreamChatUserCount"
                            ("StreamId", "UserId", "LoginId", "DisplayName", "ChatCount", "FirstChatDateTime", "LastChatDateTime", "CreatedDateTime", "UpdatedDateTime")
                        VALUES
                            ({resolvedStreamId}, {userId}, {loginId}, {displayName}, 1, {chatDateTime}, {chatDateTime}, {now}, {now})
                        ON CONFLICT ("StreamId", "UserId") DO UPDATE SET
                            "LoginId" = excluded."LoginId",
                            "DisplayName" = excluded."DisplayName",
                            "ChatCount" = "T_StreamChatUserCount"."ChatCount" + 1,
                            "FirstChatDateTime" = MIN("T_StreamChatUserCount"."FirstChatDateTime", excluded."FirstChatDateTime"),
                            "LastChatDateTime" = MAX("T_StreamChatUserCount"."LastChatDateTime", excluded."LastChatDateTime"),
                            "UpdatedDateTime" = excluded."UpdatedDateTime";
                        """);
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
