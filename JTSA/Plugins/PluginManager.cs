using JTSA.Plugin.Abstractions;
using System.Collections.ObjectModel;
using System.Reflection;
using System.Text.Json;
using System.IO;

namespace JTSA.Plugins;

public sealed class PluginManager : IDisposable
{
    public const int SupportedApiVersion = 1;
    private readonly MainWindow mainWindow;
    private bool disposed;

    public PluginManager(MainWindow mainWindow)
    {
        this.mainWindow = mainWindow;
        PluginRoot = Path.Combine(AppContext.BaseDirectory, "Plugins");
    }

    public string PluginRoot { get; }
    public ObservableCollection<PluginDescriptor> Plugins { get; } = new();
    public ObservableCollection<string> LoadErrors { get; } = new();

    public void Discover()
    {
        ShutdownLoadedPlugins();
        Plugins.Clear();
        LoadErrors.Clear();
        Directory.CreateDirectory(PluginRoot);

        foreach (var manifestPath in Directory.EnumerateFiles(
                     PluginRoot, "plugin.json", SearchOption.AllDirectories))
        {
            TryLoad(manifestPath);
        }
    }

    public void Open(PluginDescriptor descriptor)
    {
        if (descriptor.LoadedPlugin is null) return;

        try
        {
            using (descriptor.LoadedPlugin.LoadContext.EnterContextualReflection())
                descriptor.LoadedPlugin.Instance.Open();
            descriptor.Status = "起動中";
        }
        catch (Exception ex)
        {
            descriptor.Status = "起動失敗";
            RecordError(descriptor.Name, ex);
        }
    }

    private void TryLoad(string manifestPath)
    {
        PluginLoadContext? loadContext = null;
        try
        {
            var manifest = JsonSerializer.Deserialize<PluginManifest>(
                File.ReadAllText(manifestPath),
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                ?? throw new InvalidDataException("plugin.json を読み取れません。");

            ValidateManifest(manifest);
            var pluginDirectory = Path.GetDirectoryName(manifestPath)!;
            var assemblyPath = Path.GetFullPath(Path.Combine(pluginDirectory, manifest.EntryAssembly));
            if (!IsInsideDirectory(pluginDirectory, assemblyPath) || !File.Exists(assemblyPath))
                throw new FileNotFoundException("エントリDLLがプラグインフォルダ内にありません。", assemblyPath);

            loadContext = new PluginLoadContext(assemblyPath);
            var assembly = loadContext.LoadFromAssemblyPath(assemblyPath);
            var pluginType = FindPluginType(assembly, manifest.EntryType);
            IJtsaPlugin plugin;
            using (loadContext.EnterContextualReflection())
            {
                plugin = (IJtsaPlugin?)Activator.CreateInstance(pluginType)
                    ?? throw new InvalidOperationException("プラグインを生成できません。");
            }

            if (!string.Equals(plugin.Id, manifest.Id, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("plugin.json とプラグインのIDが一致しません。");
            if (Plugins.Any(item => string.Equals(item.Id, plugin.Id, StringComparison.OrdinalIgnoreCase)))
                throw new InvalidDataException($"プラグインID '{plugin.Id}' が重複しています。");

            using (loadContext.EnterContextualReflection())
                plugin.Initialize(new JtsaPluginContext(mainWindow, pluginDirectory, plugin.Id));
            var descriptor = new PluginDescriptor(plugin.Id, plugin.Name, plugin.Description, plugin.Version)
            {
                Status = "利用可能",
                LoadedPlugin = new LoadedPlugin(plugin, loadContext)
            };
            Plugins.Add(descriptor);
            loadContext = null;
        }
        catch (Exception ex)
        {
            loadContext?.Unload();
            RecordError(Path.GetFileName(Path.GetDirectoryName(manifestPath)), ex);
        }
    }

    private static void ValidateManifest(PluginManifest manifest)
    {
        if (string.IsNullOrWhiteSpace(manifest.Id) ||
            string.IsNullOrWhiteSpace(manifest.Name) ||
            string.IsNullOrWhiteSpace(manifest.EntryAssembly))
            throw new InvalidDataException("id、name、entryAssembly は必須です。");
        if (manifest.ApiVersion != SupportedApiVersion)
            throw new NotSupportedException(
                $"APIバージョン {manifest.ApiVersion} は未対応です（対応: {SupportedApiVersion}）。");
    }

    private static Type FindPluginType(Assembly assembly, string? entryType)
    {
        if (!string.IsNullOrWhiteSpace(entryType))
        {
            var selected = assembly.GetType(entryType, throwOnError: false);
            if (selected is not null && IsPluginType(selected)) return selected;
            throw new TypeLoadException($"エントリ型 '{entryType}' が見つかりません。");
        }

        var candidates = assembly.GetTypes().Where(IsPluginType).ToArray();
        return candidates.Length switch
        {
            1 => candidates[0],
            0 => throw new TypeLoadException("IJtsaPlugin の実装が見つかりません。"),
            _ => throw new TypeLoadException("実装が複数あります。entryType を指定してください。")
        };
    }

    private static bool IsPluginType(Type type) =>
        !type.IsAbstract && !type.IsInterface && typeof(IJtsaPlugin).IsAssignableFrom(type);

    private static bool IsInsideDirectory(string directory, string path)
    {
        var root = Path.GetFullPath(directory).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        return path.StartsWith(root, StringComparison.OrdinalIgnoreCase);
    }

    private void RecordError(string? source, Exception exception)
    {
        var message = $"{source ?? "不明"}: {exception.GetBaseException().Message}";
        LoadErrors.Add(message);
        mainWindow.AppLogPanel.Error("Extension", message);
    }

    private void ShutdownLoadedPlugins()
    {
        foreach (var descriptor in Plugins)
        {
            if (descriptor.LoadedPlugin is not { } loaded) continue;
            try
            {
                using (loaded.LoadContext.EnterContextualReflection())
                    loaded.Instance.Shutdown();
            }
            catch (Exception ex) { RecordError(descriptor.Name, ex); }
            loaded.LoadContext.Unload();
            descriptor.LoadedPlugin = null;
        }
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        ShutdownLoadedPlugins();
    }
}
