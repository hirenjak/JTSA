using JTSA.Plugin.Abstractions;

namespace JTSA.Plugins;

internal static class PluginChannelPointEventHub
{
    internal static event Action<ChannelPointRedemptionInfo>? ChannelPointRedeemed;

    internal static void Publish(ChannelPointRedemptionInfo redemption) =>
        ChannelPointRedeemed?.Invoke(redemption);
}
