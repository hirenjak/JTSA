using JTSA.Panels;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JTSA.Models
{
    public class M_GamePlayList
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int GamePlayListId { get; set; }

        public string GamePlayListName { get; set; }

        public required string ThumbnailCategoryUrl { get; set; }

        public int CountSelected { get; set; }

        public int SortNumber { get; set; }

        public bool IsDeleted { get; set; }

        public DateTime LastUseDateTime { get; set; }

        public DateTime CreatedDateTime { get; set; }

        public DateTime UpdatedDateTime { get; set; }


        /// <summary>
        /// SELECT * FROM M_Category ORDER BY LastUseDateTime DESC
        /// </summary>
        /// <param name="db"></param>
        /// <returns></returns>
        public static List<M_GamePlayList> SelectAllOrderbyLastUpdate()
        {
            using var db = new AppDbContext();

            List<M_GamePlayList> results = new();

            foreach (var record in db.M_GamePlayList.OrderByDescending(x => x.LastUseDateTime))
            {
                results.Add(new()
                {
                    GamePlayListId = record.GamePlayListId,
                    ThumbnailCategoryUrl = record.ThumbnailCategoryUrl,
                    CountSelected = record.CountSelected,
                    SortNumber = record.SortNumber,
                    IsDeleted = record.IsDeleted,
                    LastUseDateTime = record.LastUseDateTime,
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
        /// <returns>true：登録成功 false：既にデータがある</returns>
        public static M_GamePlayList? Insert(M_GamePlayList insertData)
        {
            using var db = new AppDbContext();

            if (db.M_GamePlayList.SingleOrDefault(x => x.GamePlayListId == insertData.GamePlayListId) == null)
            {
                var result = db.M_GamePlayList.Add(insertData);

                db.SaveChanges();

                return result.Entity;
            }

            return null;
        }


        /// <summary>
        /// Delete
        /// </summary>
        /// <param name="id"></param>
        public static void Delete(int gamePlaylistId)
        {
            using var db = new AppDbContext();
            var entity = db.M_GamePlayList.FirstOrDefault(x => x.GamePlayListId == gamePlaylistId);

            if (entity != null)
            {
                db.M_GamePlayList.Remove(entity);
                db.SaveChanges();
            }
        }
    }
}
