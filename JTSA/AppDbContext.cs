using Microsoft.EntityFrameworkCore;
using System.IO;

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
        public DbSet<M_StreamWindow> M_StreamWindowList { get; set; }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            // 実行ファイルのディレクトリ + userdata/JTSA.db
            var dbDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "userdata");
            Directory.CreateDirectory(dbDirectory); // フォルダがなければ作成
            var dbPath = Path.Combine(dbDirectory, "JTSA.db");

            optionsBuilder.UseSqlite($"Data Source={dbPath}");
        }
    }
}