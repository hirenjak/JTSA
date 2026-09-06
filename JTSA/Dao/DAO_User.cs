using JTSA.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JTSA.Dao
{
    class DAO_User
    {
        /// <summary>
        /// SELECT * FROM M_TitleText ORDER BY BroadcastId DESC
        /// </summary>
        /// <param name="db"></param>
        /// <returns></returns>
        public static List<M_User> SelectAllOrderbyBroadcastId(AppDbContext db)
        {
            List<M_User> results = new();

            foreach (var record in db.M_User.OrderByDescending(x => x.UserId))
            {
                results.Add(new()
                {
                    UserId = record.UserId,
                    LoginId = record.LoginId,
                    DisplayName = record.DisplayName,
                    ProfielImageUrl = record.ProfielImageUrl,
                    StreamingPlatform = record.StreamingPlatform,
                    StreamingUrl = record.StreamingUrl,
                    IsFriend = record.IsFriend,
                    LastUsedDateTime = record.LastUsedDateTime,
                    CreatedDateTime = record.CreatedDateTime,
                    UpdatedDateTime = record.UpdatedDateTime
                });
            }

            return results;
        }


        /// <summary>
        /// SELECT * FROM M_TitleText ORDER BY LastUseDateTime DESC
        /// </summary>
        /// <param name="db"></param>
        /// <returns></returns>
        public static List<M_User> SelectAllOrderbyLastUser()
        {
            using var db = new AppDbContext();

            List<M_User> results = new();

            foreach (var record in db.M_User
                .Where(x => x.IsFriend)
                .OrderByDescending(x => x.LastUsedDateTime))
            {
                results.Add(new()
                {
                    UserId = record.UserId,
                    LoginId = record.LoginId,
                    DisplayName = record.DisplayName,
                    ProfielImageUrl = record.ProfielImageUrl,
                    StreamingPlatform = record.StreamingPlatform,
                    StreamingUrl = record.StreamingUrl,
                    IsFriend = record.IsFriend,
                    LastUsedDateTime = record.LastUsedDateTime,
                    CreatedDateTime = record.CreatedDateTime,
                    UpdatedDateTime = record.UpdatedDateTime
                });
            }

            return results;
        }


        /// <summary>
        /// </summary>
        /// <param name="db"></param>
        /// <returns></returns>
        public static M_User SelectOneByUserId(string userId)
        {
            using var db = new AppDbContext();

            return db.M_User.SingleOrDefault(x => x.UserId == userId);
        }


        /// <summary>
        /// Insert
        /// </summary>
        /// <param name="db"></param>
        /// <param name="insertData"></param>
        /// <returns></returns>
        public static bool Insert(M_User insertData)
        {
            using var db = new AppDbContext();

            if (!db.M_User.Any(x => x.UserId == insertData.UserId))
            {
                db.M_User.Add(insertData);
            }

            int result = db.SaveChanges();

            return result > 0 ? true : false;
        }


        /// <summary>
        /// Update
        /// </summary>
        /// <param name="db"></param>
        /// <param name="insertData"></param>
        /// <returns></returns>
        public static bool Update(M_User updateData)
        {
            using var db = new AppDbContext();

            var targetRecord = SelectOneByUserId(updateData.UserId);
            updateData.CreatedDateTime = targetRecord.CreatedDateTime;

            db.M_User.Update(updateData);
            int result = db.SaveChanges();

            return result > 0 ? true : false;
        }


        /// <summary>
        /// Update：最終使用
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public static bool UpdateLastUse(string broadcastId)
        {
            var targetRecord = SelectOneByUserId(broadcastId);

            targetRecord.LastUsedDateTime = DateTime.Now;

            return Update(targetRecord);
        }

        /// <summary>プロフィールキャッシュに存在するユーザーをフレンドとして登録する。</summary>
        public static bool MarkAsFriend(string userId)
        {
            var targetRecord = SelectOneByUserId(userId);
            if (targetRecord == null) return false;

            targetRecord.IsFriend = true;
            targetRecord.LastUsedDateTime = DateTime.Now;
            targetRecord.UpdatedDateTime = DateTime.Now;

            return Update(targetRecord);
        }

        public static void InsertUpdateFriend(
            string? twitchUserId,
            string loginId,
            string displayName,
            string? profileImageUrl,
            string streamingPlatform,
            string streamingUrl,
            string? existingUserId = null)
        {
            using var db = new AppDbContext();
            var now = DateTime.Now;
            var user = !string.IsNullOrWhiteSpace(existingUserId)
                ? db.M_User.SingleOrDefault(x => x.UserId == existingUserId)
                : string.IsNullOrWhiteSpace(twitchUserId)
                    ? db.M_User.FirstOrDefault(x => x.UserId.StartsWith("manual:") && x.LoginId == loginId)
                    : db.M_User.SingleOrDefault(x => x.UserId == twitchUserId);
            if (user == null)
            {
                var userId = string.IsNullOrWhiteSpace(twitchUserId)
                    ? $"manual:{Guid.NewGuid():N}"
                    : twitchUserId;
                db.M_User.Add(new M_User
                {
                    UserId = userId,
                    LoginId = loginId,
                    DisplayName = displayName,
                    ProfielImageUrl = profileImageUrl,
                    StreamingPlatform = streamingPlatform,
                    StreamingUrl = streamingUrl,
                    IsFriend = true,
                    LastUsedDateTime = now,
                    CreatedDateTime = now,
                    UpdatedDateTime = now
                });
            }
            else
            {
                user.LoginId = loginId;
                user.DisplayName = displayName;
                user.ProfielImageUrl = profileImageUrl;
                user.StreamingPlatform = streamingPlatform;
                user.StreamingUrl = streamingUrl;
                user.IsFriend = true;
                user.LastUsedDateTime = now;
                user.UpdatedDateTime = now;
            }

            db.SaveChanges();
        }


        /// <summary>
        /// Delete
        /// </summary>
        /// <param name="id"></param>
        public static void Delete(string broadcastId)
        {
            using var db = new AppDbContext();
            var entity = db.M_User.FirstOrDefault(x => x.UserId == broadcastId);

            if (entity != null)
            {
                db.M_User.Remove(entity);
                db.SaveChanges();
            }
        }
    }
}
