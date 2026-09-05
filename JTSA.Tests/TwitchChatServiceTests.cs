using JTSA.Utility;
using TwitchLib.Client;
using Xunit;

namespace JTSA.Tests;

public class TwitchChatServiceTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CommunityGiftAndIndividualsCountOnceInEitherOrder(bool individualsFirst)
    {
        StreamSupportTracker.Reset();
        var client = new TwitchClient();
        var service = new TwitchChatService("channel", client);
        var community = GiftLine("batch", "submysterygift", "origin-a", 5);
        if (!individualsFirst) await client.OnReadLineTestAsync(community);
        for (var i = 0; i < 5; i++)
            await client.OnReadLineTestAsync(GiftLine($"individual-{i}", "subgift", "origin-a"));
        if (individualsFirst) await client.OnReadLineTestAsync(community);
        await client.OnReadLineTestAsync(community);
        Assert.Equal(5, Assert.Single(StreamSupportTracker.SubscribeUsers).GiftCount);
    }

    [Fact]
    public async Task CommunityOnlyIsCountedAndIndependentGiftStillAdds()
    {
        StreamSupportTracker.Reset();
        var client = new TwitchClient();
        var service = new TwitchChatService("channel", client);
        await client.OnReadLineTestAsync(GiftLine("batch", "submysterygift", "origin-a", 10));
        await client.OnReadLineTestAsync(GiftLine("single", "subgift", "origin-b"));
        Assert.Equal(11, Assert.Single(StreamSupportTracker.SubscribeUsers).GiftCount);
    }

    [Fact]
    public async Task AnonymousGiftAndHandlerFailureDoNotLoseFollowingGift()
    {
        StreamSupportTracker.Reset();
        var client = new TwitchClient();
        var service = new TwitchChatService("channel", client);
        service.SubscriptionReceived += () => throw new InvalidOperationException("Effect failure");
        await client.OnReadLineTestAsync(GiftLine("anonymous", "subgift", "anon-origin")
            .Replace("login=gifter", "login=ananonymousgifter")
            .Replace("user-id=2;", "user-id=274598607;"));
        await client.OnReadLineTestAsync(GiftLine("single", "subgift", "other-origin"));
        Assert.Equal(1, StreamSupportTracker.SubscribeUsers.Single(x => x.UserName == "匿名ユーザー").GiftCount);
        Assert.Equal(1, StreamSupportTracker.SubscribeUsers.Single(x => x.UserName == "gifter").GiftCount);
    }

    private static string GiftLine(string id, string type, string origin, int count = 1) =>
        $"@badge-info=;badges=;color=;display-name=Gifter;emotes=;id={id};login=gifter;mod=0;msg-id={type};msg-param-origin-id={origin};msg-param-mass-gift-count={count};msg-param-months=1;msg-param-recipient-display-name=Receiver;msg-param-recipient-id=3;msg-param-recipient-user-name=receiver;msg-param-sender-count=100;msg-param-sub-plan=1000;msg-param-sub-plan-name=Channel;room-id=1;subscriber=0;system-msg=Gift; tmi-sent-ts=1;user-id=2;user-type= :tmi.twitch.tv USERNOTICE #channel".Replace("; tmi", ";tmi");

    private const string Message = "@badge-info=;badges=;bits=100;color=;display-name=Alice;emotes=;id=test-message;mod=0;room-id=1;subscriber=0;tmi-sent-ts=1;turbo=0;user-id=2;user-type= :alice!alice@alice.tmi.twitch.tv PRIVMSG #channel :cheer100";

    [Fact]
    public async Task HandlerFailureDoesNotPreventNextChatMessage()
    {
        var client = new TwitchClient();
        var service = new TwitchChatService("channel", client);
        var calls = 0;
        service.MessageReceived += _ =>
        {
            calls++;
            if (calls == 1) throw new InvalidOperationException("Speech processing failed");
        };

        await client.OnReadLineTestAsync(Message);
        await client.OnReadLineTestAsync(Message);

        Assert.Equal(2, calls);
    }

    [Fact]
    public async Task IrcCheerIsDisplayedWithoutDuplicatingEventSubBits()
    {
        StreamSupportTracker.Reset();
        StreamSupportTracker.AddBits("Alice", 100);
        var client = new TwitchClient();
        var service = new TwitchChatService("channel", client);
        var received = false;
        service.MessageReceived += message => received = message.Bits == 100;

        await client.OnReadLineTestAsync(Message);

        Assert.True(received);
        Assert.Equal(100, StreamSupportTracker.BitsUsers.Single().BitsAmount);
    }
}
