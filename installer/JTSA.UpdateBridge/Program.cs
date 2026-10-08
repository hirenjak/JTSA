using System.Diagnostics;
using System.Reflection;
using JTSA.SetupBootstrapper;

namespace JTSA.UpdateBridge;

internal static class Program
{
    private static int Main(string[] args)
    {
        var root = Path.GetDirectoryName(Environment.ProcessPath)
            ?? throw new InvalidOperationException("更新先を特定できません。");
        try
        {
            if (args.Length > 0 && args[0].Equals("apply", StringComparison.OrdinalIgnoreCase))
            {
                WaitForApplicationExit(args);
                LegacyPluginMigration.MoveToUserData(root);
            }

            // Velopack resolves the installed app relative to the updater executable.
            // Keep the delegated binary next to Update.exe, outside the replaceable current folder.
            var updater = Path.Combine(root, "Update.bridge-stock.exe");
            if (!File.Exists(updater))
            {
                var temporaryUpdater = updater + "." + Guid.NewGuid().ToString("N") + ".tmp";
                try
                {
                    using (var embedded = Assembly.GetExecutingAssembly().GetManifestResourceStream("VelopackUpdater")
                        ?? throw new InvalidOperationException("更新ツール本体がありません。"))
                    using (var output = File.Create(temporaryUpdater))
                        embedded.CopyTo(output);
                    File.Move(temporaryUpdater, updater, overwrite: true);
                }
                finally { if (File.Exists(temporaryUpdater)) File.Delete(temporaryUpdater); }
            }

            var startInfo = new ProcessStartInfo(updater)
            {
                UseShellExecute = false,
                WorkingDirectory = root
            };
            foreach (var arg in args) startInfo.ArgumentList.Add(arg);
            using var process = Process.Start(startInfo)
                ?? throw new InvalidOperationException("更新ツールを起動できません。");
            process.WaitForExit();
            return process.ExitCode;
        }
        catch (Exception ex)
        {
            try
            {
                var logDirectory = Path.Combine(root, "UserData");
                Directory.CreateDirectory(logDirectory);
                File.AppendAllText(Path.Combine(logDirectory, "update-bridge.log"),
                    $"{DateTimeOffset.Now:O} {ex}\n");
            }
            catch { /* Preserve the original failure code even if logging is unavailable. */ }
            return 1;
        }
    }

    private static void WaitForApplicationExit(string[] args)
    {
        var index = Array.FindIndex(args, arg => arg.Equals("--waitPid", StringComparison.OrdinalIgnoreCase));
        if (index < 0 || index + 1 >= args.Length || !int.TryParse(args[index + 1], out var pid)) return;

        try
        {
            using var process = Process.GetProcessById(pid);
            if (!process.WaitForExit(120_000))
                throw new TimeoutException("アプリが終了しないため更新を中止しました。");
        }
        catch (ArgumentException) { /* The app already exited. */ }
    }
}
