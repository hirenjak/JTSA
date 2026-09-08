using JTSA.Plugin.Abstractions;
using JTSA.Utility;
using System.Windows.Interop;
using System.IO;

namespace JTSA.Plugins;

internal sealed class JtsaPluginContext : IJtsaPluginContext
{
    private readonly MainWindow mainWindow;
    private readonly string pluginId;

    public JtsaPluginContext(MainWindow mainWindow, string pluginDirectory, string pluginId)
    {
        this.mainWindow = mainWindow;
        this.pluginId = pluginId;
        PluginDirectory = pluginDirectory;
        DataDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "JTSA", "Plugins", pluginId);
        Directory.CreateDirectory(DataDirectory);
    }

    public string PluginDirectory { get; }
    public string DataDirectory { get; }
    public nint MainWindowHandle => new WindowInteropHelper(mainWindow).Handle;

    public void Log(string message) =>
        mainWindow.AppLogPanel.Success("Extension", message);

    public void LogError(string message, Exception? exception = null) =>
        mainWindow.AppLogPanel.Error(
            "Extension",
            exception is null ? message : $"{message} {exception.GetBaseException().Message}");

    public void SetExpansionOverlay(ExpansionOverlayContent content) =>
        StreamExpansionOverlayService.SetPluginOverlay(pluginId, content);

    public void RemoveExpansionOverlay(string id) =>
        StreamExpansionOverlayService.RemovePluginOverlay(pluginId, id);
}
