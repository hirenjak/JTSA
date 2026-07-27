using Microsoft.EntityFrameworkCore;
using System.IO;

namespace JTSA.Models
{
    public class AppDbContext : DbContext
    {
        public static String dbDirectory;

        public DbSet<M_TitleText> M_TitleTextList { get; set; }
        public DbSet<M_Category> M_CategoryList { get; set; }
        public DbSet<M_Friend> M_FriendList { get; set; }
        public DbSet<M_Setting> M_SettingList { get; set; }
        public DbSet<M_TitleTag> M_TitleTagList { get; set; }
        public DbSet<M_StreamWindow> M_StreamWindowList { get; set; }
        public DbSet<M_GamePlayList> M_GamePlayList { get; set; }
        public DbSet<T_GamePlayListLink> T_GamePlayListLink { get; set; }

        /// <summary>
        /// 複合キーの設定
        /// </summary>
        /// <param name="modelBuilder"></param>
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<T_GamePlayListLink>()
                .HasKey(c => new { c.GamePlayListId, c.CategoryId });
        }

        /// <summary>
        /// DBの物理ファイル位置などの設定
        /// </summary>
        /// <param name="optionsBuilder"></param>
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            // AppData\Roaming\JTSA\userdata\JTSA.db
            dbDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), // Roaming
                "JTSA", "userdata");
            Directory.CreateDirectory(dbDirectory); // フォルダがなければ作成
            var dbPath = Path.Combine(dbDirectory, "JTSA.db");

            optionsBuilder.UseSqlite($"Data Source={dbPath}");
        }
    }
}