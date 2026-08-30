using JTSA.Models;
using JTSA.Utility;
using Xunit;

namespace JTSA.Tests;

public class StreamExpansionChatPermissionTests
{
    [Fact]
    public void NoPermissionSelected_AllowsOnlyBroadcaster()
    {
        var rule = new T_StreamExpansionHeader { UpdatedDateTime = DateTime.Now };

        Assert.True(StreamExpansionService.HasChatPermission(
            rule, StreamExpansionTriggerType.Chat, User(isBroadcaster: true)));
        Assert.False(StreamExpansionService.HasChatPermission(
            rule, StreamExpansionTriggerType.Chat, User()));
    }

    [Fact]
    public void Everyone_AllowsAnyChatUser()
    {
        var rule = new T_StreamExpansionHeader
        {
            ChatPermissionEveryone = true,
            UpdatedDateTime = DateTime.Now
        };

        Assert.True(StreamExpansionService.HasChatPermission(
            rule, StreamExpansionTriggerType.Chat, User()));
    }

    [Theory]
    [InlineData(true, false, false)]
    [InlineData(false, true, false)]
    [InlineData(false, false, true)]
    public void SelectedRole_AllowsMatchingChatUser(bool moderator, bool vip, bool subscriber)
    {
        var rule = new T_StreamExpansionHeader
        {
            ChatPermissionModerator = moderator,
            ChatPermissionVip = vip,
            ChatPermissionSubscriber = subscriber,
            UpdatedDateTime = DateTime.Now
        };

        Assert.True(StreamExpansionService.HasChatPermission(
            rule,
            StreamExpansionTriggerType.Chat,
            User(isModerator: moderator, isVip: vip, isSubscriber: subscriber)));
    }

    [Fact]
    public void Permissions_DoNotRestrictNonChatTriggers()
    {
        Assert.True(StreamExpansionService.HasChatPermission(
            new T_StreamExpansionHeader { UpdatedDateTime = DateTime.Now },
            StreamExpansionTriggerType.Raid,
            null));
    }

    private static StreamExpansionChatUserContext User(
        bool isBroadcaster = false,
        bool isModerator = false,
        bool isVip = false,
        bool isSubscriber = false)
        => new(isBroadcaster, isModerator, isVip, isSubscriber);
}
