using JTSA.Utility;
using Xunit;

namespace JTSA.Tests;

public class StreamSupportTrackerTests
{
    [Fact]
    public void EventsAreAggregatedAndSorted()
    {
        StreamSupportTracker.Reset();

        StreamSupportTracker.AddBits("alice", 100);
        StreamSupportTracker.AddBits("alice", 50);
        StreamSupportTracker.AddBits("bob", 300);
        StreamSupportTracker.AddSubscription("alice", 1, "1000");
        StreamSupportTracker.AddSubscription("alice", 3, "1000");
        StreamSupportTracker.AddGiftSubscription("gifter", "2000");
        StreamSupportTracker.AddGiftSubscription("gifter", "2000", 2);
        StreamSupportTracker.AddRaid("raider", 12);
        StreamSupportTracker.AddRaid("raider", 8);
        StreamSupportTracker.AddFollow("follower");

        Assert.Equal("bob", StreamSupportTracker.BitsUsers[0].UserName);
        Assert.Equal(150, StreamSupportTracker.BitsUsers[1].BitsAmount);
        Assert.Equal(3, StreamSupportTracker.SubscribeUsers.Single(x => !x.IsGift).CumulativeMonths);
        Assert.Equal(3, StreamSupportTracker.SubscribeUsers.Single(x => x.IsGift).GiftCount);
        Assert.Equal(20, StreamSupportTracker.RaidedUsers.Single().ViewerCount);
        Assert.Equal("follower", StreamSupportTracker.FollowUsers.Single().UserName);
        Assert.Equal(
            $"bob: 300 Bits{Environment.NewLine}alice: 150 Bits",
            StreamSupportTracker.FormatBitsUsers());
        Assert.Equal(
            $"alice: 累計3か月 / Tier 1{Environment.NewLine}gifter: Tier 2のサブギフ: 3個",
            StreamSupportTracker.FormatSubscribeUsers());
        Assert.Equal("raider: 20人", StreamSupportTracker.FormatRaidUsers());
    }

    [Fact]
    public void ResetClearsCurrentStream()
    {
        StreamSupportTracker.Reset();
        StreamSupportTracker.AddBits("alice", 100);

        StreamSupportTracker.Reset();

        Assert.Empty(StreamSupportTracker.BitsUsers);
        Assert.Empty(StreamSupportTracker.SubscribeUsers);
        Assert.Empty(StreamSupportTracker.RaidedUsers);
        Assert.Empty(StreamSupportTracker.FollowUsers);
    }

    [Fact]
    public void RemoveUsersClearsOnlySpecifiedUsers()
    {
        StreamSupportTracker.Reset();
        StreamSupportTracker.AddBits("real", 50);
        StreamSupportTracker.AddBits("test-bits", 100);
        StreamSupportTracker.AddSubscription("test-subscribe");
        StreamSupportTracker.AddRaid("test-raid", 10);
        StreamSupportTracker.AddFollow("test-follow");

        StreamSupportTracker.RemoveUsers("test-bits", "test-subscribe", "test-raid", "test-follow");

        Assert.Equal("real", StreamSupportTracker.BitsUsers.Single().UserName);
        Assert.Empty(StreamSupportTracker.SubscribeUsers);
        Assert.Empty(StreamSupportTracker.RaidedUsers);
        Assert.Empty(StreamSupportTracker.FollowUsers);
    }

    [Fact]
    public void RemoveUsersByPrefixesClearsNumberedTestUsersOnly()
    {
        StreamSupportTracker.Reset();
        StreamSupportTracker.AddBits("実ユーザー", 50);
        StreamSupportTracker.AddBits("テストBitsユーザー1", 100);
        StreamSupportTracker.AddSubscription("テストサブスクユーザー2", 2, "1000");

        StreamSupportTracker.RemoveUsersByPrefixes("テストBitsユーザー", "テストサブスクユーザー");

        Assert.Equal("実ユーザー", StreamSupportTracker.BitsUsers.Single().UserName);
        Assert.Empty(StreamSupportTracker.SubscribeUsers);
    }
}
