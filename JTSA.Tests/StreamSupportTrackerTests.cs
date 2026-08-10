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
        StreamSupportTracker.AddSubscription("alice");
        StreamSupportTracker.AddSubscription("alice", 2);
        StreamSupportTracker.AddRaid("raider", 12);
        StreamSupportTracker.AddRaid("raider", 8);

        Assert.Equal("bob", StreamSupportTracker.BitsUsers[0].UserName);
        Assert.Equal(150, StreamSupportTracker.BitsUsers[1].BitsAmount);
        Assert.Equal(3, StreamSupportTracker.SubscribeUsers.Single().SubscribeAmount);
        Assert.Equal(20, StreamSupportTracker.RaidedUsers.Single().ViewerCount);
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
    }
}
