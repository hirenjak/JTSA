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
        public DbSet<M_ChannelPoint> M_ChannelPoint { get; set; }
        public DbSet<T_ChannelPointPresetHeader> T_ChannelPointPresetHeader { get; set; }
        public DbSet<T_ChannelPointPresetItem> T_ChannelPointPresetItem { get; set; }


        /// <summary>
        /// �����L�[�̐ݒ�
        /// </summary>
        /// <param name="modelBuilder"></param>
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<T_GamePlaylistItem>()
                .HasKey(c => new { c.GamePlayListId, c.CategoryId });

            modelBuilder.Entity<T_ChannelPointPresetItem>()
                .HasKey(c => new { c.PresetId, c.RewardId });
        }

        /// <summary>
        /// DB�̕����t�@�C���ʒu�Ȃǂ̐ݒ�
        /// </summary>
        /// <param name="optionsBuilder"></param>
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            // AppData\Roaming\JTSA\userdata\JTSA.db
            dbDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), // Roaming
                "JTSA", "userdata");
            Directory.CreateDirectory(dbDirectory); // �t�H���_���Ȃ���΍쐬
            var dbPath = Path.Combine(dbDirectory, "JTSA.db");

            optionsBuilder.UseSqlite($"Data Source={dbPath}");
        }
    }
}