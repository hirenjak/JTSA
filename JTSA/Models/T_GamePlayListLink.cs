using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JTSA.Models
{
    public class T_GamePlayListLink
    {
        /// <summary> [複合キー] </summary>
        public int GamePlayListId { get; set; }

        /// <summary> [複合キー] </summary>
        public required string CategoryId { get; set; }

        public int CountSelected { get; set; }

        public int SortNumber { get; set; }

        public bool IsDeleted { get; set; }

        public DateTime LastUseDateTime { get; set; }

        public DateTime CreatedDateTime { get; set; }

        public DateTime UpdatedDateTime { get; set; }


        /// <summary>
        /// SELECT * FROM T_GamePlayListLink ORDER BY Id DESC
        /// </summary>
        /// <param name="db"></param>
        /// <returns></returns>
        public static List<T_GamePlayListLink> SelectOneByCategoryId(int gamePlayListId)
        {
            using var db = new AppDbContext();

            return db.T_GamePlayListLink.Where(x => x.GamePlayListId == gamePlayListId).ToList();
        }

        /// <summary>
        /// Insert
        /// </summary>
        /// <param name="db"></param>
        /// <param name="insertData"></param>
        /// <returns>true：登録成功 false：既にデータがある</returns>
        public static bool Insert(List<T_GamePlayListLink> insertDataList)
        {
            using var db = new AppDbContext();

            foreach (var insertData in insertDataList)
            {
                if (db.T_GamePlayListLink.SingleOrDefault(x =>
                        x.GamePlayListId == insertData.GamePlayListId
                        && x.CategoryId == insertData.CategoryId)
                    == null)
                {
                    db.T_GamePlayListLink.Add(insertData);
                }
            }

            db.SaveChanges();

            return true;
        }
    }
}
