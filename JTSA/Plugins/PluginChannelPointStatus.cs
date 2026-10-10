using JTSA.Plugin.Abstractions;
using JTSA.Utility;
using System.Globalization;
using TwitchLib.Api.Helix.Models.ChannelPoints;

namespace JTSA.Plugins;

/// <summary>Twitch API の報酬・交換をプラグイン向けの情報に変換する。</summary>
internal static class PluginChannelPointStatus
{
    public static async Task<IReadOnlyList<ChannelPointRewardStatusInfo>> GetRewardStatusesAsync()
    {
        var result = await TwitchHelper.GetCustomRewardStatusesAsync();
        return result is { } value
            ? value.Rewards.Select(reward => ToInfo(reward, value.ManageableIds.Contains(reward.Id))).ToArray()
            : [];
    }

    public static async Task<IReadOnlyList<ChannelPointPendingRedemptionInfo>> GetUnfulfilledRedemptionsAsync(string rewardId)
    {
        var redemptions = await TwitchHelper.GetUnfulfilledRedemptionsAsync(rewardId);
        return redemptions?.Select(ToInfo).ToArray() ?? [];
    }

    public static ChannelPointRewardStatusInfo ToInfo(CustomReward reward, bool isManageable) => new(
        reward.Id,
        reward.Title ?? "",
        reward.Cost,
        // TwitchLib は default_image を読み込めない（setter が無い）ため、配信者が設定した画像だけを渡す
        reward.Image?.Url2x ?? "",
        reward.IsEnabled,
        reward.IsPaused,
        reward.IsInStock,
        reward.GlobalCooldownSetting?.IsEnabled == true ? reward.GlobalCooldownSetting.GlobalCooldownSeconds : 0,
        ParseTime(reward.CooldownExpiresAt),
        reward.RedemptionsRedeemedCurrentStream,
        isManageable);

    public static ChannelPointPendingRedemptionInfo ToInfo(RewardRedemption redemption) => new(
        redemption.Id,
        redemption.Reward?.Id ?? "",
        redemption.Reward?.Title ?? "",
        redemption.UserName ?? "",
        redemption.UserInput ?? "",
        redemption.RedeemedAt);

    public static DateTimeOffset? ParseTime(string? value) =>
        DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var time) ? time : null;
}
