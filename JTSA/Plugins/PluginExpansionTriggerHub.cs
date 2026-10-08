using JTSA.Plugin.Abstractions;

namespace JTSA.Plugins;

internal static class PluginExpansionTriggerHub
{
    internal static event Action<ExpansionTriggerInfo>? ExpansionTriggered;

    internal static void Publish(ExpansionTriggerInfo info)
    {
        var handlers = ExpansionTriggered;
        if (handlers is null)
            return;

        foreach (var handler in handlers.GetInvocationList())
        {
            try
            {
                ((Action<ExpansionTriggerInfo>)handler)(info);
            }
            catch
            {
                // プラグイン例外で配信拡張本体を止めない。
            }
        }
    }
}
