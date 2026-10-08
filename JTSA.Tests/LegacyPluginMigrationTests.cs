using JTSA.SetupBootstrapper;
using Xunit;

namespace JTSA.Tests;

public sealed class LegacyPluginMigrationTests
{
    [Fact]
    public void MovesLegacyPluginsIntoUserData()
    {
        var root = CreateRoot();
        try
        {
            var oldPlugin = Path.Combine(root, "current", "Plugins", "Timer");
            Directory.CreateDirectory(oldPlugin);
            File.WriteAllText(Path.Combine(oldPlugin, "plugin.json"), "legacy");

            var result = LegacyPluginMigration.MoveToUserData(root);

            Assert.Equal(Path.Combine(root, "UserData", "Plugins"), result);
            Assert.False(Directory.Exists(Path.Combine(root, "current", "Plugins")));
            Assert.Equal("legacy", File.ReadAllText(Path.Combine(result!, "Timer", "plugin.json")));
            Assert.Null(LegacyPluginMigration.MoveToUserData(root));
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [Fact]
    public void KeepsNewerPluginFilesAndPreservesLegacyBackup()
    {
        var root = CreateRoot();
        try
        {
            var oldPlugin = Path.Combine(root, "current", "Plugins", "Timer");
            var newPlugin = Path.Combine(root, "UserData", "Plugins", "Timer");
            Directory.CreateDirectory(oldPlugin);
            Directory.CreateDirectory(newPlugin);
            File.WriteAllText(Path.Combine(oldPlugin, "plugin.json"), "old");
            File.WriteAllText(Path.Combine(oldPlugin, "settings.json"), "settings");
            File.WriteAllText(Path.Combine(newPlugin, "plugin.json"), "new");

            var backup = LegacyPluginMigration.MoveToUserData(root);

            Assert.False(Directory.Exists(Path.Combine(root, "current", "Plugins")));
            Assert.Equal("new", File.ReadAllText(Path.Combine(newPlugin, "plugin.json")));
            Assert.Equal("settings", File.ReadAllText(Path.Combine(newPlugin, "settings.json")));
            Assert.Equal("old", File.ReadAllText(Path.Combine(backup!, "Timer", "plugin.json")));
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    private static string CreateRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), "JTSA-MigrationTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        return root;
    }
}
