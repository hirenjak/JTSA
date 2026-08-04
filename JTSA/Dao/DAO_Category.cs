using JTSA.Models;
using JTSA.Utility;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JTSA.Dao
{
    class DAO_Category
    {
        #region Slect

        /// <summary>
        /// プライマルキーによる単一検索
        /// </summary>
        /// <param name="categoryId">カテゴリーID</param>
        /// <returns>検索結果</returns>
        public static M_Category? SelectOneById(string categoryId)
        {
            using (var db = new AppDbContext())
            {
                return db.M_Category.SingleOrDefault(x => x.CategoryId == categoryId);
            }
        }

        #endregion

        #region Insert/Update

        /// <summary>
        /// Update：最終使用
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public static bool UpdateLastUsed(string id)
        {
            M_Category? targetRecord = SelectOneById(id);

            if (targetRecord == null) return false;

            targetRecord.LastUsedDateTime = DateTime.Now;

            return Update(targetRecord);
        }

        #endregion

        /// <summary>
        /// SELECT * FROM M_Category ORDER BY LastUseDateTime DESC
        /// </summary>
        /// <param name="db"></param>
        /// <returns></returns>
        public static List<M_Category> SelectAllOrderbyLastUser()
        {
            using var db = new AppDbContext();

            List<M_Category> results = new();

            foreach (var record in db.M_Category.OrderByDescending(x => x.LastUsedDateTime))
            {
                results.Add(new()
                {
                    CategoryId = record.CategoryId,
                    DisplayName = record.DisplayName,
                    BoxArtUrl = record.BoxArtUrl,
                    SteamUrl = record.SteamUrl,
                    SteamHeaderArtUrl = record.SteamHeaderArtUrl,
                    LastUsedDateTime = record.LastUsedDateTime,
                    CreatedDateTime = record.CreatedDateTime,
                    UpdatedDateTime = record.UpdatedDateTime
                });
            }

            return results;
        }


        public static async Task<M_Category> InsertDataCreate(string categoryId)
        {

            var selectCategory = await TwitchHelper.GetCategoryByGameId(categoryId);

            return new M_Category
            {
                CategoryId = categoryId,
                DisplayName = selectCategory.Name,
                BoxArtUrl = selectCategory.BoxArtUrl,
                SteamHeaderArtUrl = "",
                SteamUrl = "",
                LastUsedDateTime = DateTime.Now,
                CreatedDateTime = DateTime.Now,
                UpdatedDateTime = DateTime.Now
            };
        }


        /// <summary>
        /// Insert
        /// </summary>
        /// <param name="db"></param>
        /// <param name="insertData"></param>
        /// <returns>true：登録成功 false：既にデータがある</returns>
        public static bool Insert(M_Category insertData)
        {
            using var db = new AppDbContext();

            if (db.M_Category.SingleOrDefault(x => x.CategoryId == insertData.CategoryId) == null)
            {
                db.M_Category.Add(insertData);

                db.SaveChanges();

                return true;
            }

            return false;
        }


        /// <summary>
        /// Update
        /// </summary>
        /// <param name="db"></param>
        /// <param name="insertData"></param>
        /// <returns></returns>
        public static bool Update(M_Category updateData)
        {
            using var db = new AppDbContext();

            var targetRecord = SelectOneById(updateData.CategoryId);

            if (targetRecord == null) { return false; }

            updateData.CreatedDateTime = targetRecord.CreatedDateTime;

            db.M_Category.Update(updateData);
            int result = db.SaveChanges();

            return result > 0 ? true : false;
        }


        /// <summary>
        /// Delete
        /// </summary>
        /// <param name="id"></param>
        public static void Delete(string categoryId)
        {
            using var db = new AppDbContext();
            var entity = db.M_Category.FirstOrDefault(x => x.CategoryId == categoryId);

            if (entity != null)
            {
                db.M_Category.Remove(entity);
                db.SaveChanges();
            }
        }
    }
}
