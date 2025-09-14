using Microsoft.EntityFrameworkCore;

namespace JTSA.Models
{
    public class AppDbContext : DbContext
    {
        String DBName = "JTSA.db";

        public DbSet<M_TitleText> M_TitleTextList { get; set; }
        public DbSet<M_Category> M_CategoryList { get; set; }
        public DbSet<M_Friend> M_FriendList { get; set; }
        public DbSet<M_Setting> M_SettingList { get; set; }
        public DbSet<M_TitleTag> M_TitleTagList { get; set; }

        protected override void OnConfiguring(DbContextOptionsBuilder options)
            => options.UseSqlite($"Data Source={DBName}");
    }
}