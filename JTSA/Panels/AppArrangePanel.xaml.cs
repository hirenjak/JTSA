using JTSA.Forms;
using JTSA.Models;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Management;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using System.Text.Json;

namespace JTSA.Panels;

public partial class AppArrangePanel : UserControl
{
    public ObservableCollection<AppInfoForm> RegisteredApps { get; } = [];

    private readonly DispatcherTimer statusTimer = new() { Interval = TimeSpan.FromSeconds(5) };
    private bool updatingStatuses;
    private bool isLoadingAutoStartSetting;
    private bool hasAttemptedAutoStart;
    private readonly string settingsPath;
    private bool autoStartRegisteredApps;
    private readonly Action<string, bool>? statusReporter;

    public AppArrangePanel() : this(Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "JTSA", "Plugins", "jtsa.external-apps", "external-apps-settings.json"))
    {
    }

    public AppArrangePanel(string settingsPath, Action<string, bool>? statusReporter = null)
    {
        this.settingsPath = settingsPath;
        this.statusReporter = statusReporter;
        InitializeComponent();
        DataContext = this;
        Loaded += (_, _) =>
        {
            ReloadRegisteredApps();
            LoadAutoStartSetting();
            if (IsVisible) statusTimer.Start();
        };
        Unloaded += (_, _) => statusTimer.Stop();
        IsVisibleChanged += (_, _) =>
        {
            if (IsVisible && IsLoaded)
            {
                statusTimer.Start();
                _ = UpdateStatusesAsync();
            }
            else statusTimer.Stop();
        };
        statusTimer.Tick += async (_, _) => await UpdateStatusesAsync();
    }

    private void LoadAutoStartSetting()
    {
        isLoadingAutoStartSetting = true;
        AutoStartCheckBox.IsChecked = autoStartRegisteredApps;
        isLoadingAutoStartSetting = false;

        if (AutoStartCheckBox.IsChecked == true && !hasAttemptedAutoStart)
        {
            hasAttemptedAutoStart = true;
            StartRegisteredApps(autoStartOnly: true);
        }
    }

    private void ReloadRegisteredApps()
    {
        RegisteredApps.Clear();
        var settings = LoadSettings();
        autoStartRegisteredApps = settings.AutoStartRegisteredApps;
        foreach (var item in settings.Apps.OrderBy(item => item.ProcessName, StringComparer.OrdinalIgnoreCase))
        {
            RegisteredApps.Add(item);
        }
        _ = UpdateStatusesAsync();
    }

    private async Task UpdateStatusesAsync()
    {
        if (updatingStatuses || !IsVisible || !IsLoaded) return;
        updatingStatuses = true;
        var apps = RegisteredApps.ToArray();
        try
        {
            var running = await Task.Run(() => apps.ToDictionary(
                app => app,
                app =>
                {
                    try { return IsAppRunning(app); }
                    catch (InvalidOperationException) { return false; }
                    catch (System.ComponentModel.Win32Exception) { return false; }
                }));
            if (!IsVisible || !IsLoaded) return;
            foreach (var app in apps)
            {
                if (!RegisteredApps.Contains(app)) continue;
                var status = running.GetValueOrDefault(app) ? "起動中" : "停止";
                if (app.Status != status) app.Status = status;
            }
        }
        catch (Exception ex) { Debug.WriteLine($"アプリ状態取得失敗: {ex.Message}"); }
        finally { updatingStatuses = false; }
    }

    private static bool IsAppRunning(AppInfoForm app)
    {
        if (app.ListenPort > 0 && TcpListenHelper.HasListeningPort(app.ListenPort)) return true;

        if (!string.IsNullOrWhiteSpace(app.WindowTitle))
        {
            return Win32Helper.TryFindUniqueWindow(app, out _, out _);
        }

        if (AppInfoForm.IsBatchPath(app.AppExePath) && GetCmdCommandLines().Any(item =>
                CommandLineRefersTo(item.CommandLine, app.AppExePath)))
        {
            return true;
        }

        var processes = GetRuntimeProcesses(app);
        try
        {
            return processes.Length > 0;
        }
        finally
        {
            foreach (var process in processes) process.Dispose();
        }
    }

    private static Process[] GetRuntimeProcesses(AppInfoForm app)
    {
        var byId = new Dictionary<int, Process>();
        AddProcessesByName(byId, app.GetWindowProcessName());
        return [.. byId.Values];
    }

    private static Process[] GetStopProcesses(AppInfoForm app)
    {
        var byId = new Dictionary<int, Process>();
        if (string.IsNullOrWhiteSpace(app.WindowTitle))
        {
            AddProcessesByName(byId, app.GetWindowProcessName());
        }
        AddLaunchProcesses(byId, app);
        return [.. byId.Values];
    }

    private static void AddLaunchProcesses(Dictionary<int, Process> byId, AppInfoForm app)
    {
        if (string.IsNullOrWhiteSpace(app.AppExePath)) return;

        if (AppInfoForm.IsBatchPath(app.AppExePath))
        {
            foreach (var (pid, commandLine) in GetCmdCommandLines())
            {
                if (!CommandLineRefersTo(commandLine, app.AppExePath)) continue;
                try
                {
                    AddProcess(byId, Process.GetProcessById(pid));
                }
                catch (ArgumentException)
                {
                }
            }
            return;
        }

        var launchName = Path.GetFileNameWithoutExtension(app.AppExePath);
        if (string.Equals(launchName, app.GetWindowProcessName(), StringComparison.OrdinalIgnoreCase))
        {
            return;
        }
        AddProcessesByName(byId, launchName);
    }

    private static void AddProcessesByName(Dictionary<int, Process> byId, string processName)
    {
        if (string.IsNullOrWhiteSpace(processName)) return;
        foreach (var process in Process.GetProcessesByName(processName))
        {
            AddProcess(byId, process);
        }
    }

    private static void AddProcess(Dictionary<int, Process> byId, Process process)
    {
        if (byId.TryAdd(process.Id, process)) return;
        process.Dispose();
    }

    private static List<(int Pid, string CommandLine)>? cmdCommandLineCache;
    private static DateTime cmdCommandLineCacheAt;

    private static List<(int Pid, string CommandLine)> GetCmdCommandLines()
    {
        if (cmdCommandLineCache is not null && DateTime.UtcNow - cmdCommandLineCacheAt < TimeSpan.FromSeconds(2))
        {
            return cmdCommandLineCache;
        }

        var list = new List<(int Pid, string CommandLine)>();
        try
        {
            using var searcher = new ManagementObjectSearcher(
                "SELECT ProcessId, CommandLine FROM Win32_Process WHERE Name = 'cmd.exe'");
            foreach (ManagementObject item in searcher.Get())
            {
                using (item)
                {
                    if (item["CommandLine"] is not string commandLine || string.IsNullOrWhiteSpace(commandLine)) continue;
                    list.Add((Convert.ToInt32(item["ProcessId"]), commandLine));
                }
            }
        }
        catch
        {
        }

        cmdCommandLineCache = list;
        cmdCommandLineCacheAt = DateTime.UtcNow;
        return list;
    }

    private static bool CommandLineRefersTo(string commandLine, string scriptPath)
    {
        var fullPath = Path.GetFullPath(scriptPath);
        var unquoted = commandLine.Replace("\"", "", StringComparison.Ordinal);
        return unquoted.Contains(fullPath, StringComparison.OrdinalIgnoreCase)
            || commandLine.Contains(fullPath, StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryGetWindowInfo(AppInfoForm app, out AppInfoForm captured)
    {
        captured = app;
        if (!Win32Helper.TryFindUniqueWindow(app, out var hWnd, out _)) return false;
        if (!Win32Helper.TryGetRestoredWindowRect(hWnd, out var rect, out var isMinimized)) return false;
        if (isMinimized) return false;
        if (app.WindowTitleMatchMode == WindowTitleMatchMode.Exact)
        {
            captured.WindowTitle = Win32Helper.GetWindowTitle(hWnd);
        }
        captured.X = rect.Left;
        captured.Y = rect.Top;
        captured.Width = rect.Right - rect.Left;
        captured.Height = rect.Bottom - rect.Top;
        captured.IsMinimized = app.IsMinimized;
        captured.WindowProcessName = app.WindowProcessName;
        captured.WindowTitleMatchMode = app.WindowTitleMatchMode;
        captured.ListenPort = app.ListenPort;
        return true;
    }

    private void Save(AppInfoForm app)
    {
        app.WindowProcessName = AppInfoForm.NormalizeWindowProcessName(
            app.WindowProcessName, app.ProcessName, app.AppExePath);
        var settings = LoadSettings();
        settings.Apps.RemoveAll(item =>
            string.Equals(item.ProcessName, app.ProcessName, StringComparison.OrdinalIgnoreCase));
        settings.Apps.Add(app);
        SaveSettings(settings);
    }

    private ExternalAppsSettings LoadSettings()
    {
        if (!File.Exists(settingsPath)) return new();
        try
        {
            return JsonSerializer.Deserialize<ExternalAppsSettings>(File.ReadAllText(settingsPath)) ?? new();
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            Debug.WriteLine($"外部アプリ設定読込失敗: {ex.Message}");
            return new();
        }
    }

    private void SaveSettings(ExternalAppsSettings settings)
    {
        var directory = Path.GetDirectoryName(settingsPath);
        if (!string.IsNullOrWhiteSpace(directory)) Directory.CreateDirectory(directory);
        File.WriteAllText(settingsPath, JsonSerializer.Serialize(settings, new JsonSerializerOptions
        {
            WriteIndented = true
        }));
    }

    private bool Start(AppInfoForm app)
    {
        try
        {
            if (IsAppRunning(app))
            {
                app.Status = "起動中";
                ShowStatus($"すでに起動しています: {app.ProcessName}");
                return true;
            }
        }
        catch (Exception ex)
        {
            ShowStatus($"起動状態を確認できないため起動を見送りました: {ex.Message}", false);
            return false;
        }

        if (string.IsNullOrWhiteSpace(app.AppExePath) || !File.Exists(app.AppExePath))
        {
            ShowStatus($"起動ファイルが見つかりません: {app.ProcessName}", false);
            return false;
        }
        try
        {
            var hasSeparate = app.HasSeparateWindowProcess();
            Process.Start(new ProcessStartInfo
            {
                FileName = app.AppExePath,
                WorkingDirectory = Path.GetDirectoryName(app.AppExePath) ?? string.Empty,
                UseShellExecute = true,
                WindowStyle = app.IsMinimized && !hasSeparate
                    ? ProcessWindowStyle.Minimized
                    : ProcessWindowStyle.Normal
            });
            cmdCommandLineCache = null;
            if (app.IsMinimized && hasSeparate)
            {
                ScheduleWindowMove(app);
            }
            _ = UpdateStatusesAsync();
            ShowStatus($"アプリを起動しました: {app.ProcessName}");
            return true;
        }
        catch (Exception ex)
        {
            ShowStatus($"起動失敗: {ex.Message}", false);
            return false;
        }
    }

    private void Move(AppInfoForm app)
    {
        var moved = Win32Helper.SetAppWindowRect(app, out var error);
        ShowStatus(moved ? $"アプリを配置しました: {app.ProcessName}" : error, moved);
    }

    private void ScheduleWindowMove(AppInfoForm app)
    {
        var attempts = 0;
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(400) };
        timer.Tick += (_, _) =>
        {
            attempts++;
            var moved = Win32Helper.SetAppWindowRect(app, out _);
            if (!moved && attempts < 20) return;
            timer.Stop();
            if (moved)
            {
                ShowStatus($"アプリを配置しました: {app.ProcessName}");
            }
        };
        timer.Start();
    }

    private void Stop(AppInfoForm app)
    {
        var processes = GetStopProcesses(app);
        cmdCommandLineCache = null;
        var closedWindow = false;
        string windowError = "";
        if (!string.IsNullOrWhiteSpace(app.WindowTitle))
        {
            closedWindow = Win32Helper.TryCloseUniqueWindow(app, out windowError);
        }

        var closedListen = false;
        if (app.ListenPort > 0)
        {
            closedListen = TcpListenHelper.TryKillListening(app.ListenPort, out var listenError);
            if (!closedListen && !string.IsNullOrWhiteSpace(listenError))
            {
                ShowStatus(listenError, false);
                return;
            }
        }

        var closedLaunch = false;
        try
        {
            foreach (var target in processes)
            {
                using (target)
                {
                    var closed = false;
                    if (target.MainWindowHandle != IntPtr.Zero)
                    {
                        closed = target.CloseMainWindow();
                    }
                    if (!closed && !target.HasExited) target.Kill();
                    closedLaunch = true;
                }
            }
        }
        catch (Exception ex)
        {
            ShowStatus($"停止失敗: {ex.Message}", false);
            return;
        }

        if (closedWindow || closedListen || closedLaunch)
        {
            _ = UpdateStatusesAsync();
            ShowStatus($"アプリを停止しました: {app.ProcessName}");
            return;
        }

        ShowStatus(string.IsNullOrWhiteSpace(windowError)
            ? $"停止対象が見つかりません: {app.ProcessName}"
            : windowError, false);
    }

    private void ShowStatus(string message, bool success = true)
    {
        statusReporter?.Invoke(message, success);
        Debug.WriteLine(message);
    }

    private void OpenAppRegistrationButton_Click(object sender, RoutedEventArgs e)
    {
        var window = new AppRegistrationWindow
        {
            Owner = Window.GetWindow(this)
        };

        if (window.ShowDialog() != true || window.SelectedApp is not AppInfoForm app) return;

        if (TryGetWindowInfo(app, out var captured))
        {
            if (!string.IsNullOrWhiteSpace(app.AppExePath)) captured.AppExePath = app.AppExePath;
            captured.WindowProcessName = app.WindowProcessName;
            Save(captured);
        }
        else
        {
            Save(app);
        }
        ReloadRegisteredApps();
        ShowStatus($"アプリを登録しました: {app.ProcessName}");
    }

    private void StartOrMoveButton_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as Button)?.DataContext is not AppInfoForm app) return;
        if (IsAppRunning(app))
        {
            Stop(app);
            return;
        }
        Start(app);
    }

    private void MoveButton_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as Button)?.DataContext is AppInfoForm app) Move(app);
    }

    private void SavePositionButton_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as Button)?.DataContext is not AppInfoForm app)
        {
            return;
        }
        if (!TryGetWindowInfo(app, out var captured))
        {
            ShowStatus("起動中の通常表示ウィンドウが見つかりません。最小化中の位置は保存しません。", false);
            return;
        }
        Save(captured);
        ReloadRegisteredApps();
        ShowStatus($"位置を保存しました: {app.ProcessName}");
    }

    private void EditAppSettingsButton_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as Button)?.DataContext is not AppInfoForm app) return;
        var window = new AppRegistrationWindow(app)
        {
            Owner = Window.GetWindow(this)
        };
        if (window.ShowDialog() != true || window.SelectedApp is not AppInfoForm updated) return;
        Save(updated);
        ReloadRegisteredApps();
        ShowStatus($"設定を保存しました: {updated.ProcessName}");
    }

    private void StopButton_Click(object sender, RoutedEventArgs e) { if ((sender as Button)?.DataContext is AppInfoForm app) Stop(app); }
    private void DeleteButton_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as Button)?.DataContext is not AppInfoForm app) return;
        var settings = LoadSettings();
        settings.Apps.RemoveAll(item =>
            string.Equals(item.ProcessName, app.ProcessName, StringComparison.OrdinalIgnoreCase));
        SaveSettings(settings);
        ReloadRegisteredApps();
        ShowStatus($"登録を削除しました: {app.ProcessName}");
    }

    private void StartAllButton_Click(object sender, RoutedEventArgs e) => StartRegisteredApps();
    private void MoveAllButton_Click(object sender, RoutedEventArgs e)
    {
        var failed = 0;
        foreach (var app in RegisteredApps.Where(x => x.Status == "起動中"))
        {
            if (!Win32Helper.SetAppWindowRect(app)) failed++;
        }
        ShowStatus(failed == 0
            ? "登録済みアプリを一括配置しました。"
            : $"一括配置で {failed} 件失敗しました。タイトルが一致しないウィンドウはスキップしています。",
            failed == 0);
    }
    private void StopAllButton_Click(object sender, RoutedEventArgs e) { foreach (var app in RegisteredApps.Where(x => x.Status == "起動中")) Stop(app); }

    private void StartRegisteredApps(bool autoStartOnly = false)
    {
        foreach (var app in RegisteredApps.Where(app => !autoStartOnly || app.IsAutoStart))
        {
            Start(app);
        }
    }

    private void AppAutoStartCheckBox_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as CheckBox)?.DataContext is not AppInfoForm app) return;
        Save(app);
        ShowStatus(app.IsAutoStart
            ? $"自動起動を有効にしました: {app.ProcessName}"
            : $"自動起動を無効にしました: {app.ProcessName}");
    }

    private void AppStartMinimizedCheckBox_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as CheckBox)?.DataContext is not AppInfoForm app) return;
        Save(app);
        ShowStatus(app.IsMinimized
            ? $"最小化して起動を有効にしました: {app.ProcessName}"
            : $"最小化して起動を無効にしました: {app.ProcessName}");
    }

    private void AutoStartCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        if (isLoadingAutoStartSetting) return;
        autoStartRegisteredApps = AutoStartCheckBox.IsChecked == true;
        var settings = LoadSettings();
        settings.AutoStartRegisteredApps = autoStartRegisteredApps;
        SaveSettings(settings);
        ShowStatus(AutoStartCheckBox.IsChecked == true
            ? "登録済みアプリの自動起動を有効にしました。"
            : "登録済みアプリの自動起動を無効にしました。");
    }

    private sealed class ExternalAppsSettings
    {
        public bool AutoStartRegisteredApps { get; set; }
        public List<AppInfoForm> Apps { get; set; } = [];
    }
}
