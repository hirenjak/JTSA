using JTSA.Plugin.Abstractions;
using JTSA.Plugins;
using Xunit;

namespace JTSA.Tests;

public class PluginExpansionTriggerHubTests
{
    [Fact]
    public void PublishIsolatesSubscriberExceptions()
    {
        var laterRan = false;
        void Throwing(ExpansionTriggerInfo _) => throw new InvalidOperationException("plugin");
        void Later(ExpansionTriggerInfo _) => laterRan = true;

        PluginExpansionTriggerHub.ExpansionTriggered += Throwing;
        PluginExpansionTriggerHub.ExpansionTriggered += Later;
        try
        {
            PluginExpansionTriggerHub.Publish(new ExpansionTriggerInfo("Chat", "hello"));
            Assert.True(laterRan);
        }
        finally
        {
            PluginExpansionTriggerHub.ExpansionTriggered -= Throwing;
            PluginExpansionTriggerHub.ExpansionTriggered -= Later;
        }
    }
}
