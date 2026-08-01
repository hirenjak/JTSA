using JTSA.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JTSA.Dao
{
    class DAO_TitleText
    {
        /// <summary>
        /// SELECT * FROM M_TitleText ORDER BY Id DESC
        /// </summary>
        /// <param name="db"></param>
        /// <returns></returns>
        public static List<T_TitleText> SelectAllOrderbyId(AppDbContext db)
        {
            List<T_TitleText> results = new();

            foreach (var record in db.M_TitleTextList.OrderByDescending(x => x.Id))
            {
                results.Add(new()
                {
                    Id = record.Id,
                    Content = record.Content,
                    CategoryId = record.CategoryId,
                    CategoryName = record.CategoryName,
                    CategoryBoxArtUrl = record.CategoryBoxArtUrl,
                    SelectedCount = record.SelectedCount,
                    SortNumber = record.SortNumber,
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
        public static List<T_TitleText> SelectAllOrderbyLastUser(AppDbContext db)
        {
            List<T_TitleText> results = [];

            foreach (var record in db.M_TitleTextList.OrderByDescending(x => x.LastUsedDateTime))
            {
                results.Add(new()
                {
                    Id = record.Id,
                    Content = record.Content,
                    CategoryId = record.CategoryId,
                    CategoryName = record.CategoryName,
                    CategoryBoxArtUrl = record.CategoryBoxArtUrl,
                    SelectedCount = record.SelectedCount,
                    SortNumber = record.SortNumber,
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
        public static T_TitleText SelectOneById(int id)
        {
            using var db = new AppDbContext();

            return db.M_TitleTextList.Single(x => x.Id == id);
        }


        /// <summary>
        /// SELECT * FROM M_TitleText ORDER BY Id DESC
        /// </summary>
        /// <param name="db"></param>
        /// <returns></returns>
        public static List<T_TitleText> SelectSaveDataOrderbyLastUser()
        {
            using var db = new AppDbContext();

            List<T_TitleText> results = [];

            foreach (var record in db.M_TitleTextList.Where(x => x.SortNumber == 9999).OrderByDescending(x => x.UpdatedDateTime))
            {
                results.Add(new()
                {
                    Id = record.Id,
                    Content = record.Content,
                    CategoryId = record.CategoryId,
                    CategoryName = record.CategoryName,
                    CategoryBoxArtUrl = record.CategoryBoxArtUrl,
                    SelectedCount = record.SelectedCount,
                    SortNumber = record.SortNumber,
                    LastUsedDateTime = record.LastUsedDateTime,
                    CreatedDateTime = record.CreatedDateTime,
                    UpdatedDateTime = record.UpdatedDateTime
                });
            }

            return results;
        }


        /// <summary>
        /// Insert
        /// </summary>
        /// <param name="db"></param>
        /// <param name="insertData"></param>
        /// <returns></returns>
        public static bool Insert(T_TitleText insertData)
        {
            using var db = new AppDbContext();

            db.M_TitleTextList.Add(insertData);
            int result = db.SaveChanges();

            return result > 0 ? true : false;
        }


        /// <summary>
        /// Update
        /// </summary>
        /// <param name="db"></param>
        /// <param name="insertData"></param>
        /// <returns></returns>
        public static bool Update(T_TitleText updateData)
        {
            using var db = new AppDbContext();

            var targetRecord = SelectOneById(updateData.Id);
            updateData.CreatedDateTime = targetRecord.CreatedDateTime;

            db.M_TitleTextList.Update(updateData);
            int result = db.SaveChanges();

            return result > 0 ? true : false;
        }


        /// <summary>
        /// Update：最終使用
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public static bool UpdateLastUse(int id)
        {
            var targetRecord = SelectOneById(id);

            targetRecord.SelectedCount += 1;
            targetRecord.LastUsedDateTime = DateTime.Now;

            return Update(targetRecord);
        }


        /// <summary>
        /// Delete
        /// </summary>
        /// <param name="id"></param>
        public static void Delete(int id)
        {
            using var db = new AppDbContext();

            var entity = db.M_TitleTextList.FirstOrDefault(x => x.Id == id);

            if (entity != null)
            {
                db.M_TitleTextList.Remove(entity);
                db.SaveChanges();
            }
        }
    }
}
