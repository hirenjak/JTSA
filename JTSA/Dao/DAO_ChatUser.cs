using JTSA.Models;

using Microsoft.EntityFrameworkCore;

namespace JTSA.Dao
{
    class DAO_ChatUser
    {
        /// <summary>
        /// 
        /// </summary>
        /// <param name="userId"></param>
        /// <returns></returns>
        public static T_ChatUser? SelectOneByUserId(string userId)
        {
            T_ChatUser? result;

            using (var db = new AppDbContext())
            {
                result = db.T_ChatUser.AsNoTracking().FirstOrDefault(x => x.UserId == userId);
            }

            return result;
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="record"></param>
        /// <returns></returns>
        public static bool InsertUpdate(T_ChatUser record)
        {
            using (var db = new AppDbContext())
            {
                db.Database.ExecuteSqlInterpolated($"""
                    INSERT INTO "T_ChatUser"
                        ("UserId", "LoginId", "DisplayName", "IsSubscribe", "IsRaid", "TakeBits",
                         "SelectedCount", "SortNumber", "LastUsedDateTime", "CreatedDateTime", "UpdatedDateTime")
                    VALUES
                        ({record.UserId}, {record.LoginId}, {record.DisplayName}, {record.IsSubscribe},
                         {record.IsRaid}, {record.TakeBits}, {record.SelectedCount}, {record.SortNumber},
                         {record.LastUsedDateTime}, {record.CreatedDateTime}, {record.UpdatedDateTime})
                    ON CONFLICT ("UserId") DO UPDATE SET
                        "LoginId" = excluded."LoginId", "DisplayName" = excluded."DisplayName",
                        "IsSubscribe" = excluded."IsSubscribe", "IsRaid" = excluded."IsRaid",
                        "TakeBits" = excluded."TakeBits", "SelectedCount" = excluded."SelectedCount",
                        "SortNumber" = excluded."SortNumber", "LastUsedDateTime" = excluded."LastUsedDateTime",
                        "CreatedDateTime" = excluded."CreatedDateTime", "UpdatedDateTime" = excluded."UpdatedDateTime";
                    """);
            }

            return true;
        }


        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public static bool AllDelete()
        {
            using (var db = new AppDbContext())
            {
                db.T_ChatUser.ExecuteDelete();
            }
            return true;
        }
    }
}
