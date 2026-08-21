using JTSA.Dao;
using JTSA.Models;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace JTSA.Tests;

public sealed class DaoTests : IDisposable
{
    private readonly string testDirectory;

    public DaoTests()
    {
        testDirectory = Path.Combine(Path.GetTempPath(), "JTSA.Tests", Guid.NewGuid().ToString("N"));
        AppDbContext.DatabasePathOverride = Path.Combine(testDirectory, "JTSA.db");

        using var db = new AppDbContext();
        db.Database.Migrate();
    }

    [Fact]
    public void CategoryDao_InsertSelectUpdateDelete_RoundTripsData()
    {
        var created = new DateTime(2026, 1, 2, 3, 4, 5, DateTimeKind.Local);
        var category = new M_Category
        {
            CategoryId = "1234",
            DisplayName = "Test Game",
            BoxArtUrl = "https://example.test/box.jpg",
            SteamUrl = "https://store.steampowered.com/app/1234",
            LastUsedDateTime = created,
            CreatedDateTime = created,
            UpdatedDateTime = created
        };

        Assert.True(DAO_Category.Insert(category));
        Assert.False(DAO_Category.Insert(category));

        var inserted = DAO_Category.SelectOneById(category.CategoryId);
        Assert.NotNull(inserted);
        Assert.Equal("Test Game", inserted.DisplayName);

        inserted.DisplayName = "Updated Game";
        inserted.UpdatedDateTime = created.AddDays(1);
        Assert.True(DAO_Category.Update(inserted));

        var updated = DAO_Category.SelectOneById(category.CategoryId);
        Assert.NotNull(updated);
        Assert.Equal("Updated Game", updated.DisplayName);
        Assert.Equal(created, updated.CreatedDateTime);

        DAO_Category.Delete(category.CategoryId);
        Assert.Null(DAO_Category.SelectOneById(category.CategoryId));
    }

    [Fact]
    public void SettingDao_InsertUpdate_UsesSingleRowAndPreservesCreatedDate()
    {
        Assert.True(DAO_Setting.InsertUpdate(DAO_Setting.SettingName.UserName, "first"));
        var inserted = DAO_Setting.SelectOneById(DAO_Setting.SettingName.UserName);
        Assert.NotNull(inserted);
        var created = inserted.CreatedDateTime;

        Assert.True(DAO_Setting.InsertUpdate(DAO_Setting.SettingName.UserName, "second"));
        var updated = DAO_Setting.SelectOneById(DAO_Setting.SettingName.UserName);

        Assert.NotNull(updated);
        Assert.Equal("second", updated.Value);
        Assert.Equal(created, updated.CreatedDateTime);

        using var db = new AppDbContext();
        Assert.Equal(1, db.M_Setting.Count(x => x.Name == (int)DAO_Setting.SettingName.UserName));
    }

    [Fact]
    public void UserDao_MarkAsFriend_MakesCachedUserVisibleInFriendQuery()
    {
        var now = new DateTime(2026, 8, 16, 12, 0, 0, DateTimeKind.Local);
        var user = new M_User
        {
            UserId = "123456",
            LoginId = "test_user",
            DisplayName = "Test User",
            ProfielImageUrl = "https://example.test/profile.png",
            IsFriend = false,
            LastUsedDateTime = now,
            CreatedDateTime = now,
            UpdatedDateTime = now
        };

        Assert.True(DAO_User.Insert(user));
        Assert.Empty(DAO_User.SelectAllOrderbyLastUser());

        Assert.True(DAO_User.MarkAsFriend(user.UserId));

        var friend = Assert.Single(DAO_User.SelectAllOrderbyLastUser());
        Assert.Equal(user.UserId, friend.UserId);
        Assert.True(friend.IsFriend);
    }

    [Fact]
    public void DailyChatUserCount_Increment_AggregatesByDateAndUser()
    {
        var firstDay = new DateTime(2026, 8, 16, 23, 59, 0);
        var nextDay = firstDay.AddDays(1);

        DAO_DailyChatUserCount.Increment(firstDay, "user-1", "login", "Display");
        DAO_DailyChatUserCount.Increment(firstDay, "user-1", "login", "Display Updated");
        DAO_DailyChatUserCount.Increment(firstDay, "user-2", "other", "Other");
        DAO_DailyChatUserCount.Increment(nextDay, "user-1", "login", "Display Updated");

        var firstDayCounts = DAO_DailyChatUserCount.SelectByDate(firstDay);
        Assert.Equal(2, firstDayCounts.Count);
        Assert.Equal("user-1", firstDayCounts[0].UserId);
        Assert.Equal(2, firstDayCounts[0].ChatCount);
        Assert.Equal("Display Updated", firstDayCounts[0].DisplayName);

        var nextDayCount = Assert.Single(DAO_DailyChatUserCount.SelectByDate(nextDay));
        Assert.Equal(1, nextDayCount.ChatCount);
    }

    [Fact]
    public void DailyChatUserCount_Increment_HandlesConcurrentUpdatesWithoutLosingCounts()
    {
        var chatDate = new DateTime(2026, 8, 20, 10, 57, 0);
        const int incrementCount = 40;

        Parallel.For(0, incrementCount, index =>
            DAO_DailyChatUserCount.Increment(
                chatDate, "concurrent-user", "login", $"Display {index}"));

        var count = Assert.Single(DAO_DailyChatUserCount.SelectByDate(chatDate));
        Assert.Equal(incrementCount, count.ChatCount);
    }

    [Fact]
    public void RepairLegacyMigrationHistory_AddsInitialHistoryAndCreatesBackup()
    {
        var originalPath = Path.Combine(testDirectory, "JTSA.db");
        var legacyPath = Path.Combine(testDirectory, "legacy.db");
        AppDbContext.DatabasePathOverride = legacyPath;

        try
        {
            using (var connection = new SqliteConnection($"Data Source={legacyPath};Pooling=False"))
            {
                connection.Open();
                using var command = connection.CreateCommand();
                command.CommandText = """
                    CREATE TABLE M_CategoryList (Id INTEGER);
                    CREATE TABLE M_FriendList (Id INTEGER);
                    CREATE TABLE M_SettingList (Id INTEGER);
                    CREATE TABLE M_TitleTagList (Id INTEGER);
                    CREATE TABLE T_GamePlaylistHeader (Id INTEGER);
                    CREATE TABLE T_GamePlaylistItem (Id INTEGER);
                    CREATE TABLE T_TitleTextList (Id INTEGER);
                    """;
                command.ExecuteNonQuery();
            }

            using var db = new AppDbContext();
            db.RepairLegacyMigrationHistory();

            using var verification = new SqliteConnection($"Data Source={legacyPath};Pooling=False");
            verification.Open();
            using var verifyCommand = verification.CreateCommand();
            verifyCommand.CommandText = """
                SELECT COUNT(*) FROM "__EFMigrationsHistory"
                WHERE "MigrationId" = '20260801233859_Initial';
                """;
            Assert.Equal(1L, Convert.ToInt64(verifyCommand.ExecuteScalar()));
            Assert.Single(Directory.GetFiles(testDirectory, "legacy.db.backup-*"));
        }
        finally
        {
            AppDbContext.DatabasePathOverride = originalPath;
        }
    }

    public void Dispose()
    {
        AppDbContext.DatabasePathOverride = null;

        if (Directory.Exists(testDirectory))
        {
            Directory.Delete(testDirectory, recursive: true);
        }
    }
}
