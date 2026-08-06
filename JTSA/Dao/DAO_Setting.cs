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
            ChatNotificationVolume = 5,
            JoinChatVolume = 6,
        }

        /// <summary>
        /// SELECT * FROM M_TitleText ORDER BY Id DESC
        /// </summary>
        /// <param name="db"></param>
        /// <returns></returns>
        public static M_Setting? SelectOneById(SettingName id)
        {
            using var db = new AppDbContext();
            if (db.M_Setting.Count() == 0) return null;

            return db.M_Setting.SingleOrDefault(x => x.Name == (int)id);
        }


        /// <summary>
        /// Insert
        /// </summary>
        /// <param name="db"></param>
        /// <param name="insertData"></param>
        /// <returns></returns>
        public static bool InsertUpdate(int name, string value)
        {
            using var db = new AppDbContext();

            var count = db.M_Setting.Count(x => x.Name == name);

            if (count == 0)
            {
                db.M_Setting.AddRange(new M_Setting { 
                    Name = name,
                    Value = value,
                    UpdatedDateTime = DateTime.Now,
                    CreatedDateTime = DateTime.Now,
                    LastUsedDateTime = DateTime.Now
                });
            }
            else
            {
                db.M_Setting.UpdateRange(new M_Setting
                {
                    Name = name,
                    Value = value,
                    UpdatedDateTime = DateTime.Now,
                    LastUsedDateTime = DateTime.Now
                });
            }
    ;
            int result = db.SaveChanges();

            return result > 0 ? true : false;
        }
    }
}
