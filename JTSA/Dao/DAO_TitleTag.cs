using JTSA.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JTSA.Dao
{
    class DAO_TitleTag
    {
        /// <summary>
        /// SELECT * FROM M_Category ORDER BY LastUseDateTime DESC
        /// </summary>
        /// <param name="db"></param>
        /// <returns></returns>
        public static List<M_TitleTag> SelectAllOrderbyLastUser()
        {
            using var db = new AppDbContext();

            List<M_TitleTag> results = new();

            foreach (var record in db.M_TitleTag.OrderByDescending(x => x.LastUsedDateTime))
            {
                results.Add(new()
                {
                    Id = record.Id,
                    DisplayName = record.DisplayName,
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
        public static M_TitleTag SelectOneById(long id)
        {
            using var db = new AppDbContext();

            return db.M_TitleTag.Single(x => x.Id == id);
        }


        /// <summary>
        /// Delete
        /// </summary>
        /// <param name="id"></param>
        public static void Delete(long id)
        {
            using var db = new AppDbContext();

            var entity = db.M_TitleTag.FirstOrDefault(x => x.Id == id);

            if (entity != null)
            {
                db.M_TitleTag.Remove(entity);
                db.SaveChanges();
            }
        }


        /// <summary>
        /// Insert
        /// </summary>
        /// <param name="db"></param>
        /// <param name="insertData"></param>
        /// <returns></returns>
        public static bool Insert(M_TitleTag insertData)
        {
            using var db = new AppDbContext();

            db.M_TitleTag.Add(insertData);
            int result = db.SaveChanges();

            return result > 0 ? true : false;
        }


        /// <summary>
        /// Update
        /// </summary>
        /// <param name="db"></param>
        /// <param name="insertData"></param>
        /// <returns></returns>
        public static bool Update(M_TitleTag updateData)
        {
            using var db = new AppDbContext();

            var targetRecord = SelectOneById(updateData.Id);

            updateData.CreatedDateTime = targetRecord.CreatedDateTime;

            db.M_TitleTag.Update(updateData);
            int result = db.SaveChanges();

            return result > 0 ? true : false;
        }


        /// <summary>
        /// Update：最終使用
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public static bool UpdateLastUse(long id)
        {
            var targetRecord = SelectOneById(id);

            targetRecord.SelectedCount += 1;
            targetRecord.LastUsedDateTime = DateTime.Now;

            return Update(targetRecord);
        }
    }
}
