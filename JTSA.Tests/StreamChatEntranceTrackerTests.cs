using JTSA.Utility;
using Xunit;

namespace JTSA.Tests;

public class StreamChatEntranceTrackerTests
{
    [Fact]
    public void TryEnter_ReturnsTrueOncePerUserAndStream()
    {
        var tracker = new StreamChatEntranceTracker();

        Assert.True(tracker.TryEnter("stream-a", "user-1"));
        Assert.False(tracker.TryEnter("stream-a", "user-1"));
        Assert.True(tracker.TryEnter("stream-a", "user-2"));
        Assert.True(tracker.TryEnter("stream-b", "user-1"));
        Assert.False(tracker.TryEnter("stream-b", "user-1"));
    }

    [Fact]
    public void Clear_StartsEntranceTrackingAgain()
    {
        var tracker = new StreamChatEntranceTracker();
        Assert.True(tracker.TryEnter("stream-a", "user-1"));

        tracker.Clear();

        Assert.True(tracker.TryEnter("stream-a", "user-1"));
    }

    [Fact]
    public void TryEnter_RejectsMissingUserId()
    {
        var tracker = new StreamChatEntranceTracker();

        Assert.False(tracker.TryEnter("stream-a", ""));
        Assert.False(tracker.TryEnter("stream-a", null));
    }

    [Fact]
    public void TryEnter_DoesNotTreatOfflineChatAsAStreamEntrance()
    {
        var tracker = new StreamChatEntranceTracker();

        Assert.False(tracker.TryEnter("", "user-1"));
        Assert.False(tracker.TryEnter(null, "user-1"));
        Assert.True(tracker.TryEnter("stream-a", "user-1"));
    }

    [Fact]
    public void Restore_PreventsPreviouslyPersistedUsersFromEnteringAgain()
    {
        var tracker = new StreamChatEntranceTracker();

        tracker.Restore("stream-a", ["user-1", "user-2"]);

        Assert.False(tracker.TryEnter("stream-a", "user-1"));
        Assert.False(tracker.TryEnter("stream-a", "user-2"));
        Assert.True(tracker.TryEnter("stream-a", "user-3"));
        Assert.True(tracker.TryEnter("stream-b", "user-1"));
    }
}
