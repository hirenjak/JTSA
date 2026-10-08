using System.IO;

namespace JTSA.SetupBootstrapper;

public static class LegacyPluginMigration
{
    public static string? MoveToUserData(string installRoot)
    {
        var source = Path.Combine(installRoot, "current", "Plugins");
        if (!Directory.Exists(source)) return null;

        var userData = Path.Combine(installRoot, "UserData");
        var destination = Path.Combine(userData, "Plugins");
        Directory.CreateDirectory(userData);

        if (!Directory.Exists(destination))
        {
            Directory.Move(source, destination);
            return destination;
        }

        // 既存のUserDataを上書きせず、旧フォルダもバックアップとして残す。
        var backup = Path.Combine(userData, "PluginBackups", Guid.NewGuid().ToString("N"), "Plugins");
        Directory.CreateDirectory(Path.GetDirectoryName(backup)!);
        Directory.Move(source, backup);
        try
        {
            foreach (var directory in Directory.EnumerateDirectories(backup, "*", SearchOption.AllDirectories))
            {
                if ((File.GetAttributes(directory) & FileAttributes.ReparsePoint) != 0)
                    throw new IOException($"リンクを含むプラグインフォルダは移行できません: {directory}");
            }

            foreach (var file in Directory.EnumerateFiles(backup, "*", SearchOption.AllDirectories))
            {
                if ((File.GetAttributes(file) & FileAttributes.ReparsePoint) != 0)
                    throw new IOException($"リンクを含むプラグインファイルは移行できません: {file}");
                var target = Path.Combine(destination, Path.GetRelativePath(backup, file));
                if (File.Exists(target)) continue;
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                File.Copy(file, target);
            }
        }
        catch
        {
            Directory.Move(backup, source);
            throw;
        }

        return backup;
    }
}
