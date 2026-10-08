using JTSA.Plugins;
using Newtonsoft.Json;
using TwitchLib.Api.Helix.Models.ChannelPoints;
using Xunit;

namespace JTSA.Tests;

public class PluginChannelPointStatusTests
{
    private static CustomReward Reward(string extra) => JsonConvert.DeserializeObject<CustomReward>($$"""
        {"id":"r1","title":"筋トレ","cost":300,"is_enabled":true,"is_paused":false,"is_in_stock":true,
         "global_cooldown_setting":{"is_enabled":true,"global_cooldown_seconds":270}{{extra}}}
        """)!;

    [Fact]
    public void CooldownRewardIsNotRedeemableUntilItExpires()
    {
        var info = PluginChannelPointStatus.ToInfo(
            Reward(""","cooldown_expires_at":"2026-02-26T18:20:00Z","redemptions_redeemed_current_stream":5"""), true);

        Assert.Equal(270, info.GlobalCooldownSeconds);
        Assert.Equal(new DateTimeOffset(2026, 2, 26, 18, 20, 0, TimeSpan.Zero), info.CooldownExpiresAt);
        Assert.Equal(5, info.RedemptionsRedeemedCurrentStream);
        Assert.True(info.IsManageable);
        Assert.True(info.IsCoolingDown(new DateTimeOffset(2026, 2, 26, 18, 19, 0, TimeSpan.Zero)));
        Assert.False(info.IsRedeemable(new DateTimeOffset(2026, 2, 26, 18, 19, 0, TimeSpan.Zero)));
        Assert.True(info.IsRedeemable(new DateTimeOffset(2026, 2, 26, 18, 21, 0, TimeSpan.Zero)));
    }

    [Fact]
    public void RewardWithoutCooldownIsRedeemableUnlessPaused()
    {
        var info = PluginChannelPointStatus.ToInfo(Reward(""","cooldown_expires_at":null"""), false);
        Assert.Null(info.CooldownExpiresAt);
        Assert.True(info.IsRedeemable(DateTimeOffset.UtcNow));
        Assert.False((info with { IsPaused = true }).IsRedeemable(DateTimeOffset.UtcNow));
        Assert.False((info with { IsInStock = false }).IsRedeemable(DateTimeOffset.UtcNow));
    }

    [Fact]
    public void PendingRedemptionKeepsIdsForCompletion()
    {
        var redemption = JsonConvert.DeserializeObject<RewardRedemption>("""
            {"id":"x1","user_name":"viewer","user_input":"スクワット","status":"UNFULFILLED",
             "redeemed_at":"2026-02-26T18:15:00Z","reward":{"id":"r1","title":"筋トレ","cost":300}}
            """)!;
        var info = PluginChannelPointStatus.ToInfo(redemption);
        Assert.Equal(("x1", "r1", "筋トレ", "viewer", "スクワット"),
            (info.Id, info.RewardId, info.RewardTitle, info.UserName, info.UserInput));
        Assert.Equal(new DateTimeOffset(2026, 2, 26, 18, 15, 0, TimeSpan.Zero), info.RedeemedAt);
    }
}
