using JTSA.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static M_Setting;

namespace JTSA.Dao
{
    class DAO_Setting
    {
        public enum SettingName : int
        {
            UserName = 1,
            RefreshToken = 2,
            ExpiresIn = 3,
            FriendPrefixWord = 4,
        }

        /// <summary>
        /// SELECT * FROM M_TitleText ORDER BY Id DESC
        /// </summary>
        /// <param name="db"></param>
        /// <returns></returns>
        public static M_Setting? SelectOneById(SettingName id)
        {
            using var db = new AppDbContext();
            if (db.M_SettingList.Count() == 0) return null;

            return db.M_SettingList.Single(x => x.Name == (int)id);
        }


        /// <summary>
        /// Insert
        /// </summary>
        /// <param name="db"></param>
        /// <param name="insertData"></param>
        /// <returns></returns>
        public static bool InsertUpdate(M_Setting insertData)
        {
            using var db = new AppDbContext();

            var count = db.M_SettingList.Count(x => x.Name == insertData.Name);
            //db.M_SettingList.UpdateRange(insertData);

            if (count == 0)
            {
                db.M_SettingList.AddRange(insertData);
            }
            else
            {
                db.M_SettingList.UpdateRange(insertData);
            }
    ;
            int result = db.SaveChanges();

            return result > 0 ? true : false;
        }
    }
}
