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
    public void StreamExpansionClip_RoundTripsWithOtherActions()
    {
        var id = DAO_StreamExpansion.Save(new T_StreamExpansionHeader { Name = "Clips", IsActive = true, UpdatedDateTime = DateTime.Now },
        [
            new T_StreamExpansionItem { ActionType = "ObsClip", Content = "{chat_login}", SortNumber = 2, Weight = 3, UpdatedDateTime = DateTime.Now },
            new T_StreamExpansionItem { ActionType = "ObsText", Content = "Hello", ObsSourceName = "title", SortNumber = 2, Weight = 3, UpdatedDateTime = DateTime.Now }
        ]);
        var items = DAO_StreamExpansion.SelectItems(id);
        Assert.Equal(2, items.Count);
        var clip = Assert.Single(items, item => item.ActionType == "ObsClip");
        Assert.Equal("{chat_login}", clip.Content);
        Assert.Equal(2, clip.SortNumber);
        Assert.Equal(3, clip.Weight);
    }

    [Fact]
    public void StreamExpansionHourly_RoundTripsAfterMigration()
    {
        var header = new T_StreamExpansionHeader
        {
            Name = "Hourly", IsActive = true, UpdatedDateTime = DateTime.Now
        };
        var id = DAO_StreamExpansion.Save(header, []);
        var saved = Assert.Single(DAO_StreamExpansion.SelectAllHeaders());
        Assert.False(saved.IsHourly);
        saved.IsHourly = true;
        saved.IsAdStart = true;
        saved.IsAdEnd = true;
        saved.IsAdUpcoming = true;
        saved.AdAdvanceMinutes = 3;
        saved.IsScheduledTime = true;
        saved.ScheduledHour = 23;
        saved.ScheduledMinute = 55;
        DAO_StreamExpansion.Save(saved, []);
        Assert.True(Assert.Single(DAO_StreamExpansion.SelectAllHeaders()).IsHourly);
        var scheduled = Assert.Single(DAO_StreamExpansion.SelectAllHeaders());
        Assert.True(scheduled.IsAdStart && scheduled.IsAdEnd && scheduled.IsAdUpcoming);
        Assert.Equal(3, scheduled.AdAdvanceMinutes);
        Assert.True(scheduled.IsScheduledTime);
        Assert.Equal(23, scheduled.ScheduledHour);
        Assert.Equal(55, scheduled.ScheduledMinute);
        saved.IsHourly = false;
        DAO_StreamExpansion.Save(saved, []);
        Assert.False(Assert.Single(DAO_StreamExpansion.SelectAllHeaders()).IsHourly);
    }

    [Fact]
    public void ParticipationStore_PersistsAccountsAndClearsWithoutReplayingRedemptions()
    {
        var participant = new JTSA.Forms.ParticipationUserForm("user", "参加者", "入力", new DateTime(2026, 9, 4, 12, 0, 0))
        { ProfileImageUrl = "https://example.test/icon.png", ParticipationCount = 3 };
        JTSA.Utility.ParticipationStore.Save("account-a", [participant], ["redemption-a"]);
        JTSA.Utility.ParticipationStore.Save("account-b", [participant with { UserId = "other" }], ["redemption-b"]);
        Assert.Equal(participant, Assert.Single(JTSA.Utility.ParticipationStore.Load("account-a").Users));
        Assert.Equal("other", Assert.Single(JTSA.Utility.ParticipationStore.Load("account-b").Users).UserId);
        JTSA.Utility.ParticipationStore.Save("account-a", [], ["redemption-a"], [participant]);
        var playing = JTSA.Utility.ParticipationStore.Load("account-a");
        Assert.Empty(playing.Users);
        Assert.Equal(participant, Assert.Single(playing.PlayingUsers));
        JTSA.Utility.ParticipationStore.Save("account-a", [], ["redemption-a"]);
        var restored = JTSA.Utility.ParticipationStore.Load("account-a");
        Assert.Empty(restored.Users);
        Assert.Empty(restored.PlayingUsers);
        Assert.Equal(3, JTSA.Utility.ParticipationStore.GetParticipationCount("account-a", "user"));
        Assert.Equal(0, JTSA.Utility.ParticipationStore.GetParticipationCount("account-b", "user"));
        var returning = participant with
        {
            ParticipationCount = JTSA.Utility.ParticipationStore.GetParticipationCount("account-a", "user"),
            MatchCount = 0
        };
        JTSA.Utility.ParticipationStore.Save("account-a", [returning], ["redemption-a"]);
        Assert.Equal(3, Assert.Single(JTSA.Utility.ParticipationStore.Load("account-a").Users).ParticipationCount);
        Assert.Equal("redemption-a", Assert.Single(restored.RedemptionIds));
        Assert.Single(JTSA.Utility.ParticipationStore.Load("account-b").Users);
        JTSA.Utility.ParticipationStore.Save("account-a", [], ["redemption-a"], [returning], slotCount: 4);
        JTSA.Utility.ParticipationStore.Clear("account-a");
        var cleared = JTSA.Utility.ParticipationStore.Load("account-a");
        Assert.Empty(cleared.Users);
        Assert.Empty(cleared.PlayingUsers);
        Assert.Empty(cleared.ParticipationCounts);
        Assert.Equal(4, cleared.SlotCount);
        Assert.Equal("redemption-a", Assert.Single(cleared.RedemptionIds));
        Assert.Equal(0, JTSA.Utility.ParticipationStore.GetParticipationCount("account-a", "user"));
        Assert.Equal(3, JTSA.Utility.ParticipationStore.GetParticipationCount("account-b", "other"));
    }

    [Fact]
    public void CategoryDao_InsertSelectUpdateDelete_RoundTripsData()
    {
        var created = new DateTime(2026, 1, 2, 3, 4, 5, DateTimeKind.Local);
        var category = new M_Category
        {
            CategoryId = "1234",
            DisplayName = "Test Game",
            JapaneseDisplayName = "テストゲーム",
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
        Assert.Equal("テストゲーム", inserted.JapaneseDisplayName);

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
    public void TitleTagDao_MissingId_ReturnsNullOrFalse()
    {
        Assert.Null(DAO_TitleTag.SelectOneById(long.MaxValue));
        Assert.False(DAO_TitleTag.UpdateLastUse(long.MaxValue));
        Assert.False(DAO_TitleTag.Update(new M_TitleTag
        {
            Id = long.MaxValue,
            DisplayName = "Missing",
            UpdatedDateTime = DateTime.Now
        }));
    }

    [Fact]
    public void TitleTagDao_UpdateThenDelete_RejectsStaleRecord()
    {
        var created = new DateTime(2026, 1, 2, 3, 4, 5);
        var tag = new M_TitleTag
        {
            DisplayName = "Test tag",
            CreatedDateTime = created,
            LastUsedDateTime = created,
            UpdatedDateTime = created
        };
        Assert.True(DAO_TitleTag.Insert(tag));
        Assert.True(DAO_TitleTag.UpdateLastUse(tag.Id));
        var updated = DAO_TitleTag.SelectOneById(tag.Id);
        Assert.NotNull(updated);
        Assert.Equal(1, updated.SelectedCount);
        Assert.True(updated.LastUsedDateTime > created);

        updated.DisplayName = "Updated tag";
        updated.CreatedDateTime = created.AddDays(1);
        Assert.True(DAO_TitleTag.Update(updated));
        var saved = DAO_TitleTag.SelectOneById(tag.Id);
        Assert.NotNull(saved);
        Assert.Equal("Updated tag", saved.DisplayName);
        Assert.Equal(created, saved.CreatedDateTime);

        DAO_TitleTag.Delete(tag.Id);
        Assert.Null(DAO_TitleTag.SelectOneById(tag.Id));
        Assert.False(DAO_TitleTag.UpdateLastUse(tag.Id));
        Assert.False(DAO_TitleTag.Update(updated));
        Assert.Null(DAO_TitleTag.SelectOneById(tag.Id));
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
    public void StreamChatUserCount_Increment_AggregatesAcrossDatesInSameStream()
    {
        var firstDay = new DateTime(2026, 8, 16, 23, 59, 0);
        var nextDay = firstDay.AddDays(1);

        DAO_StreamChatUserCount.Increment(firstDay, "user-1", "login", "Display", "stream-1");
        DAO_StreamChatUserCount.Increment(firstDay, "user-1", "login", "Display Updated", "stream-1");
        DAO_StreamChatUserCount.Increment(firstDay, "user-2", "other", "Other", "stream-1");
        DAO_StreamChatUserCount.Increment(nextDay, "user-1", "login", "Display Updated", "stream-1");

        var counts = DAO_StreamChatUserCount.SelectByStreamId("stream-1");
        Assert.Equal(2, counts.Count);
        Assert.Equal("user-1", counts[0].UserId);
        Assert.Equal(3, counts[0].ChatCount);
        Assert.Equal(firstDay, counts[0].FirstChatDateTime);
        Assert.Equal(nextDay, counts[0].LastChatDateTime);
        Assert.Equal("Display Updated", counts[0].DisplayName);
    }

    [Fact]
    public void StreamChatUserCount_Increment_HandlesConcurrentUpdatesWithoutLosingCounts()
    {
        var chatDate = new DateTime(2026, 8, 20, 10, 57, 0);
        const int incrementCount = 40;

        Parallel.For(0, incrementCount, index =>
            DAO_StreamChatUserCount.Increment(
                chatDate, "concurrent-user", "login", $"Display {index}", "stream-concurrent"));

        var count = Assert.Single(DAO_StreamChatUserCount.SelectByStreamId("stream-concurrent"));
        Assert.Equal(incrementCount, count.ChatCount);
    }

    [Fact]
    public void StreamChatUserCount_Increment_SeparatesSameUserByStreamId()
    {
        var chatDate = new DateTime(2026, 8, 24, 12, 0, 0);

        DAO_StreamChatUserCount.Increment(chatDate, "user-1", "login", "Display", "stream-a");
        DAO_StreamChatUserCount.Increment(chatDate, "user-1", "login", "Display", "stream-a");
        DAO_StreamChatUserCount.Increment(chatDate, "user-1", "login", "Display", "stream-b");

        var streamA = Assert.Single(DAO_StreamChatUserCount.SelectByStreamId("stream-a"));
        var streamB = Assert.Single(DAO_StreamChatUserCount.SelectByStreamId("stream-b"));
        Assert.Equal(2, streamA.ChatCount);
        Assert.Equal(1, streamB.ChatCount);
        Assert.Equal("user-1", streamA.UserId);
        Assert.Equal("user-1", streamB.UserId);
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
