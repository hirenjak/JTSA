using JTSA.Models;

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
            using var db = new AppDbContext();
            var record = db.T_DailyChatUserCount.SingleOrDefault(
                x => x.ChatDate == date && x.UserId == userId);

            if (record == null)
            {
                db.T_DailyChatUserCount.Add(new T_DailyChatUserCount
                {
                    ChatDate = date,
                    UserId = userId,
                    LoginId = loginId,
                    DisplayName = displayName,
                    ChatCount = 1,
                    CreatedDateTime = now,
                    UpdatedDateTime = now
                });
            }
            else
            {
                record.LoginId = loginId;
                record.DisplayName = displayName;
                record.ChatCount++;
                record.UpdatedDateTime = now;
            }

            db.SaveChanges();
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
