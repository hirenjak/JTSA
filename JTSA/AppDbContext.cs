using Microsoft.EntityFrameworkCore;
using System.IO;

namespace JTSA.Models
{
    public class AppDbContext : DbContext
    {
        public static string dbDirectory = string.Empty;

        // DAO tests can redirect the database without touching the user's AppData DB.
        internal static string? DatabasePathOverride { get; set; }

        public DbSet<T_TitleText> T_TitleText { get; set; }
        public DbSet<M_Category> M_Category { get; set; }
        public DbSet<M_User> M_User { get; set; }
        public DbSet<M_Setting> M_Setting { get; set; }
        public DbSet<M_TitleTag> M_TitleTag { get; set; }
        public DbSet<T_GamePlaylistHeader> T_GamePlaylistHeader { get; set; }
        public DbSet<T_GamePlaylistItem> T_GamePlaylistItem { get; set; }
        public DbSet<T_ChatUser> T_ChatUser { get; set; }
        public DbSet<M_ChannelPoint> M_ChannelPoint { get; set; }
        public DbSet<T_ChannelPointPresetHeader> T_ChannelPointPresetHeader { get; set; }
        public DbSet<T_ChannelPointPresetItem> T_ChannelPointPresetItem { get; set; }
        internal DbSet<T_StreamExpansionHeader> T_StreamExpansionHeader { get; set; }
        internal DbSet<T_StreamExpansionItem> T_StreamExpansionItem { get; set; }
        internal DbSet<T_StreamWindow> T_StreamWindow { get; set; }


        /// <summary>
        /// 複合キーの設定
        /// </summary>
        /// <param name="modelBuilder"></param>
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<T_GamePlaylistItem>()
                .HasKey(c => new { c.GamePlayListId, c.CategoryId });

            modelBuilder.Entity<T_ChannelPointPresetItem>()
                .HasKey(c => new { c.PresetId, c.RewardId });

            modelBuilder.Entity<T_StreamExpansionItem>()
                .HasIndex(c => new { c.Id, c.HeaderId });
        }

        /// <summary>
        /// DBの保存ファイル位置などの設定
        /// </summary>
        /// <param name="optionsBuilder"></param>
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            // AppData\Roaming\JTSA\userdata\JTSA.db
            var dbPath = DatabasePathOverride;
            var isTestDatabase = !string.IsNullOrWhiteSpace(dbPath);
            if (string.IsNullOrWhiteSpace(dbPath))
            {
                dbDirectory = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), // Roaming
                    "JTSA", "userdata");
                Directory.CreateDirectory(dbDirectory); // フォルダがなければ作成
                dbPath = Path.Combine(dbDirectory, "JTSA.db");
            }
            else
            {
                dbDirectory = Path.GetDirectoryName(dbPath) ?? string.Empty;
                if (!string.IsNullOrEmpty(dbDirectory))
                {
                    Directory.CreateDirectory(dbDirectory);
                }
            }

            optionsBuilder.UseSqlite(
                isTestDatabase
                    ? $"Data Source={dbPath};Pooling=False"
                    : $"Data Source={dbPath}");
        }
    }
}
