using JTSA.Models;

namespace JTSA.Dao
{
    static class DAO_TwitchAccount
    {
        public static List<M_TwitchAccount> SelectAll()
        {
            using var db = new AppDbContext();
            return db.M_TwitchAccount.OrderByDescending(x => x.IsPrimary).ThenBy(x => x.UserName).ToList();
        }

        public static M_TwitchAccount? SelectById(long id)
        {
            using var db = new AppDbContext();
            return db.M_TwitchAccount.SingleOrDefault(x => x.Id == id);
        }

        public static M_TwitchAccount InsertUpdate(string userName, string broadcasterId, string refreshToken, bool isPrimary = false)
        {
            using var db = new AppDbContext();
            var account = db.M_TwitchAccount.SingleOrDefault(x => x.BroadcasterId == broadcasterId);
            var now = DateTime.Now;
            if (account is null)
            {
                account = new M_TwitchAccount
                {
                    UserName = userName,
                    BroadcasterId = broadcasterId,
                    RefreshToken = refreshToken,
                    IsPrimary = isPrimary || !db.M_TwitchAccount.Any(),
                    CreatedDateTime = now,
                    UpdatedDateTime = now,
                    LastUsedDateTime = now
                };
                db.M_TwitchAccount.Add(account);
            }
            else
            {
                account.UserName = userName;
                account.RefreshToken = refreshToken;
                account.IsPrimary = account.IsPrimary || isPrimary;
                account.UpdatedDateTime = now;
                account.LastUsedDateTime = now;
            }
            db.SaveChanges();
            return account;
        }

        public static void UpdateRefreshToken(long id, string refreshToken)
        {
            using var db = new AppDbContext();
            var account = db.M_TwitchAccount.Single(x => x.Id == id);
            account.RefreshToken = refreshToken;
            account.UpdatedDateTime = DateTime.Now;
            db.SaveChanges();
        }

        public static bool DeleteSubAccount(long id)
        {
            using var db = new AppDbContext();
            var account = db.M_TwitchAccount.SingleOrDefault(x => x.Id == id);
            if (account is null || account.IsPrimary)
                return false;
            db.M_TwitchAccount.Remove(account);
            return db.SaveChanges() > 0;
        }
    }
}
