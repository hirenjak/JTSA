using JTSA.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JTSA.Dao
{
    class DAO_Friend
    {
        /// <summary>
        /// SELECT * FROM M_TitleText ORDER BY BroadcastId DESC
        /// </summary>
        /// <param name="db"></param>
        /// <returns></returns>
        public static List<M_Friend> SelectAllOrderbyBroadcastId(AppDbContext db)
        {
            List<M_Friend> results = new();

            foreach (var record in db.M_FriendList.OrderByDescending(x => x.BroadcastId))
            {
                results.Add(new()
                {
                    BroadcastId = record.BroadcastId,
                    UserId = record.UserId,
                    DisplayName = record.DisplayName,
                    IsDeleted = record.IsDeleted,
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
        public static List<M_Friend> SelectAllOrderbyLastUser()
        {
            using var db = new AppDbContext();

            List<M_Friend> results = new();

            foreach (var record in db.M_FriendList.OrderByDescending(x => x.LastUsedDateTime))
            {
                results.Add(new()
                {
                    BroadcastId = record.BroadcastId,
                    UserId = record.UserId,
                    DisplayName = record.DisplayName,
                    IsDeleted = record.IsDeleted,
                    LastUsedDateTime = record.LastUsedDateTime,
                    CreatedDateTime = record.CreatedDateTime,
                    UpdatedDateTime = record.UpdatedDateTime
                });
            }

            return results;
        }


        /// <summary>
        /// SELECT * FROM M_TitleText ORDER BY Id DESC
        /// </summary>
        /// <param name="db"></param>
        /// <returns></returns>
        public static M_Friend SelectOneByBroadcasterId(string broadcasterId)
        {
            using var db = new AppDbContext();

            return db.M_FriendList.Single(x => x.BroadcastId == broadcasterId);
        }


        /// <summary>
        /// Insert
        /// </summary>
        /// <param name="db"></param>
        /// <param name="insertData"></param>
        /// <returns></returns>
        public static bool Insert(M_Friend insertData)
        {
            using var db = new AppDbContext();

            if (!db.M_FriendList.Any(x => x.BroadcastId == insertData.BroadcastId))
            {
                db.M_FriendList.Add(insertData);
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
        public static bool Update(M_Friend updateData)
        {
            using var db = new AppDbContext();

            var targetRecord = SelectOneByBroadcasterId(updateData.BroadcastId);
            updateData.CreatedDateTime = targetRecord.CreatedDateTime;

            db.M_FriendList.Update(updateData);
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
            var targetRecord = SelectOneByBroadcasterId(broadcastId);

            targetRecord.LastUsedDateTime = DateTime.Now;

            return Update(targetRecord);
        }


        /// <summary>
        /// Delete
        /// </summary>
        /// <param name="id"></param>
        public static void Delete(string broadcastId)
        {
            using var db = new AppDbContext();
            var entity = db.M_FriendList.FirstOrDefault(x => x.BroadcastId == broadcastId);

            if (entity != null)
            {
                db.M_FriendList.Remove(entity);
                db.SaveChanges();
            }
        }
    }
}
