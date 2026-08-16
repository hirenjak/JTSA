using JTSA.Dao;
using JTSA.Models;
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

    public void Dispose()
    {
        AppDbContext.DatabasePathOverride = null;

        if (Directory.Exists(testDirectory))
        {
            Directory.Delete(testDirectory, recursive: true);
        }
    }
}
