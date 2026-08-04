using JTSA.Models;

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
                result = db.T_ChatUser.FirstOrDefault(x => x.UserId == userId);
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
                var existingRecord = db.T_ChatUser.FirstOrDefault(x => x.UserId == record.UserId);

                if (existingRecord != null)
                {
                    db.T_ChatUser.Update(record);
                }
                else
                {
                    db.T_ChatUser.Add(record);
                }

                db.SaveChanges();
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
                db.T_ChatUser.RemoveRange(db.T_ChatUser);

                db.SaveChanges();
            }
            return true;
        }
    }
}
