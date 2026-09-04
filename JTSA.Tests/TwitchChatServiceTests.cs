using JTSA.Utility;
using TwitchLib.Client;
using Xunit;

namespace JTSA.Tests;

public class TwitchChatServiceTests
{
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
