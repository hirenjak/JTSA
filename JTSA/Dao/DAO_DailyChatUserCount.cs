using JTSA.Models;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace JTSA.Dao
{
    class DAO_DailyChatUserCount
    {
        /// <summary>指定日のユーザー別チャット数を1件加算する。</summary>
        public static void Increment(DateTime chatDate, string userId, string loginId, string displayName)
        {
            if (string.IsNullOrWhiteSpace(userId)) return;

            var date = chatDate.Date;
            var now = DateTime.Now;
            const int maxAttempts = 4;

            for (var attempt = 1; ; attempt++)
            {
                try
                {
                    using var db = new AppDbContext();
                    db.Database.ExecuteSqlInterpolated($"""
                        INSERT INTO "T_DailyChatUserCount"
                            ("ChatDate", "UserId", "LoginId", "DisplayName", "ChatCount", "CreatedDateTime", "UpdatedDateTime")
                        VALUES
                            ({date}, {userId}, {loginId}, {displayName}, 1, {now}, {now})
                        ON CONFLICT ("ChatDate", "UserId") DO UPDATE SET
                            "LoginId" = excluded."LoginId",
                            "DisplayName" = excluded."DisplayName",
                            "ChatCount" = "T_DailyChatUserCount"."ChatCount" + 1,
                            "UpdatedDateTime" = excluded."UpdatedDateTime";
                        """);
                    return;
                }
                catch (SqliteException ex) when (ex.SqliteErrorCode is 5 or 6 && attempt < maxAttempts)
                {
                    // チャットクリアや拡張機能の保存と重なった場合だけ、短時間待って再試行する。
                    Thread.Sleep(50 * attempt);
                }
            }
        }

        /// <summary>指定日のユーザー別チャット数を多い順に取得する。</summary>
        public static List<T_DailyChatUserCount> SelectByDate(DateTime chatDate)
        {
            var date = chatDate.Date;
            using var db = new AppDbContext();
            return db.T_DailyChatUserCount
                .Where(x => x.ChatDate == date)
                .OrderByDescending(x => x.ChatCount)
                .ThenBy(x => x.DisplayName)
                .ToList();
        }
    }
}
