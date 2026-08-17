using Microsoft.EntityFrameworkCore;
using Microsoft.Data.Sqlite;
using System.Data;
using System.IO;

namespace JTSA.Models
{
    public class AppDbContext : DbContext
    {
        private const string InitialMigrationId = "20260801233859_Initial";
        private static readonly string[] LegacyInitialTables =
        {
            "M_CategoryList",
            "M_FriendList",
            "M_SettingList",
            "M_TitleTagList",
            "T_GamePlaylistHeader",
            "T_GamePlaylistItem",
            "T_TitleTextList"
        };

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
        public DbSet<T_DailyChatUserCount> T_DailyChatUserCount { get; set; }
        public DbSet<M_ChannelPoint> M_ChannelPoint { get; set; }
        public DbSet<T_ChannelPointPresetHeader> T_ChannelPointPresetHeader { get; set; }
        public DbSet<T_ChannelPointPresetItem> T_ChannelPointPresetItem { get; set; }
        internal DbSet<T_StreamExpansionHeader> T_StreamExpansionHeader { get; set; }
        internal DbSet<T_StreamExpansionItem> T_StreamExpansionItem { get; set; }
        internal DbSet<T_StreamWindow> T_StreamWindow { get; set; }

        /// <summary>
        /// EF Core導入前に作成された旧DBへ初期マイグレーション履歴を補完する。
        /// 旧テーブルが一式そろっている場合に限り、DBをバックアップしてから補正する。
        /// </summary>
        internal void RepairLegacyMigrationHistory()
        {
            var connection = (SqliteConnection)Database.GetDbConnection();
            var shouldClose = connection.State == ConnectionState.Closed;

            try
            {
                if (shouldClose)
                {
                    connection.Open();
                }

                var existingLegacyTables = LegacyInitialTables.Count(table => TableExists(connection, table));
                if (existingLegacyTables == 0 || MigrationExists(connection, InitialMigrationId))
                {
                    return;
                }

                if (existingLegacyTables != LegacyInitialTables.Length)
                {
                    throw new InvalidOperationException(
                        "旧形式のデータベースが不完全なため、自動更新できませんでした。" +
                        "JTSA.dbをバックアップしてからサポートへ連絡してください。");
                }

                BackupDatabase(connection.DataSource);

                using var transaction = connection.BeginTransaction();
                using var command = connection.CreateCommand();
                command.Transaction = transaction;
                command.CommandText = """
                    CREATE TABLE IF NOT EXISTS "__EFMigrationsHistory" (
                        "MigrationId" TEXT NOT NULL CONSTRAINT "PK___EFMigrationsHistory" PRIMARY KEY,
                        "ProductVersion" TEXT NOT NULL
                    );
                    INSERT OR IGNORE INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
                    VALUES ('20260801233859_Initial', '9.0.9');
                    """;
                command.ExecuteNonQuery();
                transaction.Commit();
            }
            finally
            {
                if (shouldClose)
                {
                    connection.Close();
                }
            }
        }

        private static bool TableExists(SqliteConnection connection, string tableName)
        {
            using var command = connection.CreateCommand();
            command.CommandText =
                "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = $name;";
            command.Parameters.AddWithValue("$name", tableName);
            return Convert.ToInt64(command.ExecuteScalar()) > 0;
        }

        private static bool MigrationExists(SqliteConnection connection, string migrationId)
        {
            if (!TableExists(connection, "__EFMigrationsHistory"))
            {
                return false;
            }

            using var command = connection.CreateCommand();
            command.CommandText =
                "SELECT COUNT(*) FROM \"__EFMigrationsHistory\" WHERE \"MigrationId\" = $id;";
            command.Parameters.AddWithValue("$id", migrationId);
            return Convert.ToInt64(command.ExecuteScalar()) > 0;
        }

        private static void BackupDatabase(string databasePath)
        {
            if (string.IsNullOrWhiteSpace(databasePath) || !File.Exists(databasePath))
            {
                return;
            }

            var backupPath = $"{databasePath}.backup-{DateTime.Now:yyyyMMddHHmmss}";
            File.Copy(databasePath, backupPath, overwrite: false);
        }


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

            modelBuilder.Entity<T_DailyChatUserCount>()
                .HasKey(c => new { c.ChatDate, c.UserId });

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
