using System.IO;

namespace JTSA.Plugins;

internal static class PluginStorage
{
    private static readonly string UserDataRoot = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "JTSA", "UserData");

    public static string PluginRoot => Path.Combine(UserDataRoot, "Plugins");

    public static void MigrateInstalledPlugins()
    {
        Directory.CreateDirectory(PluginRoot);

        // 旧バージョンで永続化したユーザープラグインを先に移行する。
        CopyPluginDirectoriesWithoutDeleting(
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "JTSA", "Plugins"),
            overwriteExisting: false);

        // アプリに同梱された標準プラグインは、更新版のファイルだけ上書きする。
        CopyPluginDirectoriesWithoutDeleting(
            Path.Combine(AppContext.BaseDirectory, "Plugins"),
            overwriteExisting: true);
    }

    public static string GetDataDirectory(string pluginId)
    {
        if (pluginId.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 ||
            pluginId.Contains(Path.DirectorySeparatorChar) ||
            pluginId.Contains(Path.AltDirectorySeparatorChar))
            throw new InvalidDataException($"プラグインID '{pluginId}' を保存先に使用できません。");

        var destination = Path.Combine(UserDataRoot, "PluginData", pluginId);
        var legacyDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "JTSA", "Plugins", pluginId);
        CopyDirectoryWithoutDeleting(legacyDirectory, destination, overwriteExisting: false);
        Directory.CreateDirectory(destination);
        return destination;
    }

    private static void CopyPluginDirectoriesWithoutDeleting(string sourceRoot, bool overwriteExisting)
    {
        if (!Directory.Exists(sourceRoot) || PathsEqual(sourceRoot, PluginRoot)) return;

        foreach (var manifestPath in Directory.EnumerateFiles(
                     sourceRoot, "plugin.json", SearchOption.AllDirectories))
        {
            var sourceDirectory = Path.GetDirectoryName(manifestPath)!;
            var relativeDirectory = Path.GetRelativePath(sourceRoot, sourceDirectory);
            var destinationDirectory = Path.Combine(PluginRoot, relativeDirectory);
            if (!overwriteExisting && Directory.Exists(destinationDirectory)) continue;
            CopyDirectoryWithoutDeleting(sourceDirectory, destinationDirectory, overwriteExisting);
        }
    }

    private static void CopyDirectoryWithoutDeleting(
        string sourceRoot,
        string destinationRoot,
        bool overwriteExisting)
    {
        if (!Directory.Exists(sourceRoot) || PathsEqual(sourceRoot, destinationRoot)) return;

        Directory.CreateDirectory(destinationRoot);
        foreach (var sourcePath in Directory.EnumerateFiles(sourceRoot, "*", SearchOption.AllDirectories))
        {
            var relativePath = Path.GetRelativePath(sourceRoot, sourcePath);
            var destinationPath = Path.Combine(destinationRoot, relativePath);
            if (!overwriteExisting && File.Exists(destinationPath)) continue;
            Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);
            File.Copy(sourcePath, destinationPath, overwriteExisting);
        }
    }

    private static bool PathsEqual(string first, string second) => string.Equals(
        Path.GetFullPath(first).TrimEnd(Path.DirectorySeparatorChar),
        Path.GetFullPath(second).TrimEnd(Path.DirectorySeparatorChar),
        StringComparison.OrdinalIgnoreCase);
}
