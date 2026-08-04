using Microsoft.EntityFrameworkCore;
using System.IO;

namespace JTSA.Models
{
    public class AppDbContext : DbContext
    {
        public static string dbDirectory;

        public DbSet<T_TitleText> T_TitleText { get; set; }
        public DbSet<M_Category> M_Category { get; set; }
        public DbSet<M_User> M_User { get; set; }
        public DbSet<M_Setting> M_Setting { get; set; }
        public DbSet<M_TitleTag> M_TitleTag { get; set; }
        public DbSet<T_GamePlaylistHeader> T_GamePlaylistHeader { get; set; }
        public DbSet<T_GamePlaylistItem> T_GamePlaylistItem { get; set; }
        public DbSet<T_ChatUser> T_ChatUser { get; set; }
        

        /// <summary>
        /// 複合キーの設定
        /// </summary>
        /// <param name="modelBuilder"></param>
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<T_GamePlaylistItem>()
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