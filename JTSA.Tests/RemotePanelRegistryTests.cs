using JTSA.Plugin.Abstractions;
using JTSA.Utility;
using Xunit;

namespace JTSA.Tests;

public sealed class RemotePanelRegistryTests
{
    [Fact]
    public void InteractivePanel_ExposesStateAndRoutesOnlyRegisteredActions()
    {
        var pluginId = "test-" + Guid.NewGuid().ToString("N");
        var panelId = $"plugin:{pluginId}:timer";
        string? received = null;
        try
        {
            RemotePanelRegistry.Set(pluginId, new RemotePluginPanelContent("timer", "タイマー", "<p>timer</p>"),
                (action, value) => { received = $"{action}:{value}"; return true; }, () => "{\"Running\":true}");
            Assert.Equal("{\"Running\":true}", Assert.Single(RemotePanelRegistry.GetPanels(),
                panel => panel.Id == panelId).State);
            Assert.True(RemotePanelRegistry.ApplyAction(panelId, "start", "5"));
            Assert.Equal("start:5", received);
            RemotePanelRegistry.RemoveAll(pluginId);
            Assert.False(RemotePanelRegistry.ApplyAction(panelId, "start", "5"));
        }
        finally { RemotePanelRegistry.RemoveAll(pluginId); }
    }

    [Fact]
    public void SavedOrder_ReordersBuiltInsAndPlugins_AndKeepsNewPanels()
    {
        var pluginId = "test-" + Guid.NewGuid().ToString("N");
        var panelId = $"plugin:{pluginId}:sample";
        try
        {
            RemotePanelRegistry.SetOrder(["todo", panelId, "obs", "todo", "missing"]);
            RemotePanelRegistry.Set(pluginId, new RemotePluginPanelContent("sample", "追加", ""));
            Assert.Equal(["todo", panelId, "obs", "chat"],
                RemotePanelRegistry.GetPanels().Select(panel => panel.Id));
            RemotePanelRegistry.RemoveAll(pluginId);
            Assert.Equal(["todo", "obs", "chat"],
                RemotePanelRegistry.GetPanels().Select(panel => panel.Id));
        }
        finally
        {
            RemotePanelRegistry.RemoveAll(pluginId);
            RemotePanelRegistry.SetOrder([]);
        }
    }

    [Fact]
    public void PluginPanels_AppearWithBuiltIns_AndAreRemovedByPlugin()
    {
        var pluginId = "test-" + Guid.NewGuid().ToString("N");
        try
        {
            RemotePanelRegistry.Set(pluginId,
                new RemotePluginPanelContent("sample", "追加パネル", "<p>content</p>"));

            var panels = RemotePanelRegistry.GetPanels();
            Assert.Equal(["obs", "chat", "todo"], panels.Take(3).Select(panel => panel.Id));
            var added = Assert.Single(panels, panel => panel.Id == $"plugin:{pluginId}:sample");
            Assert.Equal("追加パネル", added.Title);
            Assert.True(added.IsPlugin);

            RemotePanelRegistry.RemoveAll(pluginId);
            Assert.DoesNotContain(RemotePanelRegistry.GetPanels(), panel => panel.Id == added.Id);
        }
        finally
        {
            RemotePanelRegistry.RemoveAll(pluginId);
        }
    }
}
