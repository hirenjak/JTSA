using System.Diagnostics;
using System.Reflection;
using System.Windows.Forms;

namespace JTSA.SetupBootstrapper;

internal static class Program
{
    [STAThread]
    private static async Task<int> Main(string[] args)
    {
        try
        {
            var installRoot = ResolveInstallRoot(args);
            var runningApps = Process.GetProcessesByName("JTSA");
            try
            {
                if (runningApps.Length > 0)
                {
                    MessageBox.Show("JTSAを終了してからセットアップを再実行してください。",
                        "JTSA セットアップ", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return 1;
                }
            }
            finally { foreach (var process in runningApps) process.Dispose(); }

            var current = Path.Combine(installRoot, "current");
            if (Directory.Exists(current))
            {
                var updater = Path.Combine(installRoot, "Update.exe");
                if (!File.Exists(updater))
                    throw new InvalidOperationException("既存のJTSA更新ツールが見つかりません。インストール先を確認してください。");

                var version = Assembly.GetExecutingAssembly().GetName().Version?.ToString(3)
                    ?? throw new InvalidOperationException("セットアップのバージョンを取得できません。");
                var installedExecutable = Path.Combine(current, "JTSA.exe");
                if (File.Exists(installedExecutable) &&
                    Version.TryParse(FileVersionInfo.GetVersionInfo(installedExecutable).FileVersion,
                        out var installedVersion) &&
                    installedVersion >= Version.Parse(version))
                {
                    LegacyPluginMigration.MoveToUserData(installRoot);
                    return 0;
                }

                var package = await EnsureUpdatePackageAsync(installRoot, version);
                var migratedPath = LegacyPluginMigration.MoveToUserData(installRoot);
                using var update = Process.Start(new ProcessStartInfo(updater)
                {
                    UseShellExecute = false,
                    WorkingDirectory = installRoot,
                    ArgumentList = { "apply", "--package", package }
                }) ?? throw new InvalidOperationException("更新ツールを起動できませんでした。");
                update.WaitForExit();
                if (update.ExitCode != 0)
                    throw new InvalidOperationException(
                        $"更新に失敗しました（終了コード {update.ExitCode}）。\nプラグインの退避先: {migratedPath ?? "なし"}");
                return 0;
            }

            using var setup = Assembly.GetExecutingAssembly().GetManifestResourceStream("VelopackSetup")
                ?? throw new InvalidOperationException("セットアップ本体が見つかりません。");
            var temporaryDirectory = Path.Combine(Path.GetTempPath(), "JTSA-Setup", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(temporaryDirectory);
            var setupPath = Path.Combine(temporaryDirectory, "JTSA-win-Setup.exe");
            try
            {
                using (var output = File.Create(setupPath)) setup.CopyTo(output);
                var startInfo = new ProcessStartInfo(setupPath)
                {
                    UseShellExecute = false,
                    WorkingDirectory = temporaryDirectory
                };
                foreach (var argument in args) startInfo.ArgumentList.Add(argument);
                using var installer = Process.Start(startInfo)
                    ?? throw new InvalidOperationException("セットアップを起動できませんでした。");
                installer.WaitForExit();
                if (installer.ExitCode != 0)
                    throw new InvalidOperationException($"セットアップが終了コード {installer.ExitCode} で停止しました。");
                return 0;
            }
            finally
            {
                try { File.Delete(setupPath); Directory.Delete(temporaryDirectory); }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.GetBaseException().Message, "JTSA セットアップ",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
            return 1;
        }
    }

    private static async Task<string> EnsureUpdatePackageAsync(string installRoot, string version)
    {
        var packages = Path.Combine(installRoot, "packages");
        var package = Path.Combine(packages, $"JTSA-{version}-full.nupkg");
        if (File.Exists(package)) return package;

        Directory.CreateDirectory(packages);
        var temporary = package + "." + Guid.NewGuid().ToString("N") + ".partial";
        try
        {
            using var client = new HttpClient { Timeout = TimeSpan.FromMinutes(20) };
            var url = $"https://github.com/hirenjak/JTSA/releases/download/v{version}/JTSA-{version}-full.nupkg";
            using var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
            response.EnsureSuccessStatusCode();
            await using (var stream = File.Create(temporary))
                await response.Content.CopyToAsync(stream);
            File.Move(temporary, package, overwrite: true);
            return package;
        }
        finally
        {
            if (File.Exists(temporary)) File.Delete(temporary);
        }
    }

    private static string ResolveInstallRoot(string[] args)
    {
        var index = Array.FindIndex(args, value => string.Equals(
            value, "--installto", StringComparison.OrdinalIgnoreCase));
        if (index >= 0)
        {
            if (index + 1 >= args.Length) throw new ArgumentException("--installto の保存先が指定されていません。");
            return Path.GetFullPath(args[index + 1]);
        }

        return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "JTSA");
    }
}
