using JTSA.Dao;
using JTSA.Models;
using JTSA.Utility;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace JTSA.Tests;

public sealed class StreamSupportPersistenceTests : IDisposable
{
    private readonly string testDirectory = Path.Combine(Path.GetTempPath(), "JTSA.Tests", Guid.NewGuid().ToString("N"));

    public StreamSupportPersistenceTests()
    {
        AppDbContext.DatabasePathOverride = Path.Combine(testDirectory, "JTSA.db");
        using var db = new AppDbContext();
        db.Database.Migrate();
        StreamSupportTracker.Reset();
    }

    [Fact]
    public void RestartRestoresAllSupportAndContinuesAccumulating()
    {
        StreamSupportTracker.StartStream("stream-a");
        StreamSupportTracker.AddBits("alice", 100);
        StreamSupportTracker.AddSubscription("subscriber", 6, "2000");
        StreamSupportTracker.AddGiftSubscription("gifter", "3000", 3);
        StreamSupportTracker.AddRaid("raider", 12);
        StreamSupportTracker.AddFollow("follower");
        var followedAt = StreamSupportTracker.FollowUsers.Single().FollowedAt;

        // プロセス再起動と同じく、メモリを破棄してDBから復元する。
        StreamSupportTracker.Reset();
        StreamSupportTracker.StartStream("stream-a");

        Assert.Equal(100, StreamSupportTracker.BitsUsers.Single().BitsAmount);
        var subscription = StreamSupportTracker.SubscribeUsers.Single(x => !x.IsGift);
        Assert.Equal(6, subscription.CumulativeMonths);
        Assert.Equal("2", subscription.Tier);
        Assert.Equal(3, StreamSupportTracker.SubscribeUsers.Single(x => x.IsGift).GiftCount);
        Assert.Equal(12, StreamSupportTracker.RaidedUsers.Single().ViewerCount);
        Assert.Equal(followedAt, StreamSupportTracker.FollowUsers.Single().FollowedAt);

        StreamSupportTracker.AddBits("alice", 50);
        StreamSupportTracker.AddGiftSubscription("gifter", "3000", 2);
        StreamSupportTracker.StartStream("stream-a");
        StreamSupportTracker.Reset();
        StreamSupportTracker.StartStream("stream-a");
        Assert.Equal(150, StreamSupportTracker.BitsUsers.Single().BitsAmount);
        Assert.Equal(5, StreamSupportTracker.SubscribeUsers.Single(x => x.IsGift).GiftCount);
    }

    [Fact]
    public void AnotherStreamDoesNotOverwriteEarlierStream()
    {
        StreamSupportTracker.StartStream("stream-a");
        StreamSupportTracker.AddBits("alice", 100);
        StreamSupportTracker.StartStream("stream-b");
        Assert.Empty(StreamSupportTracker.BitsUsers);
        StreamSupportTracker.AddBits("bob", 200);

        StreamSupportTracker.Reset();
        StreamSupportTracker.StartStream("stream-a");
        Assert.Equal("alice", StreamSupportTracker.BitsUsers.Single().UserName);
        Assert.Equal(100, StreamSupportTracker.BitsUsers.Single().BitsAmount);
        StreamSupportTracker.StartStream("stream-b");
        Assert.Equal("bob", StreamSupportTracker.BitsUsers.Single().UserName);
    }

    [Fact]
    public void OfflineAndRestartPreserveSavedStreamAndUserRemovals()
    {
        StreamSupportTracker.StartStream("stream-a");
        StreamSupportTracker.AddBits("alice", 100);
        StreamSupportTracker.AddBits("test-user", 10);
        StreamSupportTracker.RemoveUsers(bitsUserName: "test-user");
        StreamSupportTracker.StartStream(string.Empty);
        StreamSupportTracker.Reset();
        StreamSupportTracker.StartStream("stream-a");
        Assert.Equal("alice", StreamSupportTracker.BitsUsers.Single().UserName);
    }

    [Fact]
    public void LegacySnapshotSurvivesSavingAnotherStream()
    {
        DAO_Setting.InsertUpdate(DAO_Setting.SettingName.StreamSupportSnapshot,
            """{"StreamId":"legacy-stream","BitsUsers":[{"UserName":"alice","BitsAmount":123}]}""");
        StreamSupportTracker.StartStream("new-stream");
        StreamSupportTracker.AddBits("bob", 20);
        StreamSupportTracker.Reset();
        StreamSupportTracker.StartStream("legacy-stream");
        Assert.Equal(123, StreamSupportTracker.BitsUsers.Single().BitsAmount);
    }

    [Fact]
    public void MalformedSnapshotDoesNotPreventSavingNewEvents()
    {
        DAO_Setting.InsertUpdate(DAO_Setting.SettingName.StreamSupportSnapshot, "{broken");
        StreamSupportTracker.StartStream("stream-a");
        StreamSupportTracker.AddBits("alice", 100);
        StreamSupportTracker.Reset();
        StreamSupportTracker.StartStream("stream-a");
        Assert.Equal(100, StreamSupportTracker.BitsUsers.Single().BitsAmount);
    }

    public void Dispose()
    {
        StreamSupportTracker.Reset();
        AppDbContext.DatabasePathOverride = null;
        if (Directory.Exists(testDirectory)) Directory.Delete(testDirectory, recursive: true);
    }
}
