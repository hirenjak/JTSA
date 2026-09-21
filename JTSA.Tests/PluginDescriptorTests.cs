using JTSA.Plugins;
using Xunit;

namespace JTSA.Tests;

public class PluginDescriptorTests
{
    [Fact]
    public void IsAutoStart_NotifiesPersistenceCallbackOnlyWhenChanged()
    {
        var changes = new List<bool>();
        var descriptor = new PluginDescriptor(
            "sample.plugin", "Sample", "", new Version(1, 0),
            isAutoStart: true,
            (_, enabled) => changes.Add(enabled));

        descriptor.IsAutoStart = true;
        descriptor.IsAutoStart = false;
        descriptor.IsAutoStart = false;
        descriptor.IsAutoStart = true;

        Assert.Equal([false, true], changes);
    }
}
