using JTSA.Dao;
using JTSA.Forms;
using JTSA.Models;
using Microsoft.Win32;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace JTSA.Panels;

public partial class AppArrangePanel : UserControl
{
    public ObservableCollection<AppInfoForm> RegisteredApps { get; } = [];

    private readonly DispatcherTimer statusTimer = new() { Interval = TimeSpan.FromSeconds(5) };
    private bool updatingStatuses;
    private bool isLoadingAutoStartSetting;
    private bool hasAttemptedAutoStart;

    public AppArrangePanel()
    {
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

    private MainWindow? MainWindow => Application.Current.MainWindow as MainWindow;

    private void LoadAutoStartSetting()
    {
        isLoadingAutoStartSetting = true;
        var setting = DAO_Setting.SelectOneById(DAO_Setting.SettingName.AutoStartRegisteredApps);
        AutoStartCheckBox.IsChecked = bool.TryParse(setting?.Value, out var enabled) && enabled;
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
        foreach (var item in DAO_StreamWindow.SelectAll())
        {
            RegisteredApps.Add(ToForm(item));
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
            var running = await Task.Run(() =>
            {
                var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var process in Process.GetProcesses())
                {
                    using (process)
                    {
                        try { names.Add(process.ProcessName); }
                        catch (InvalidOperationException) { }
                        catch (System.ComponentModel.Win32Exception) { }
                    }
                }
                return names;
            });
            if (!IsVisible || !IsLoaded) return;
            foreach (var app in apps)
            {
                if (!RegisteredApps.Contains(app)) continue;
                var status = running.Contains(app.ProcessName) ? "起動中" : "停止";
                if (app.Status != status) app.Status = status;
            }
        }
        catch (Exception ex) { Debug.WriteLine($"アプリ状態取得失敗: {ex.Message}"); }
        finally { updatingStatuses = false; }
    }

    private static AppInfoForm ToForm(T_StreamWindow item) => new()
    {
        ProcessName = item.ProcessName,
        WindowTitle = item.WindowTitle,
        AppExePath = item.AppExePath,
        X = item.X,
        Y = item.Y,
        Width = item.Width,
        Height = item.Height,
        IsAutoStart = item.IsAutoStart,
        IsMinimized = item.IsMinimized
    };

    private static bool IsAppRunning(AppInfoForm app)
    {
        // OBSなどはタイトルが変化し、トレイ格納時にはウィンドウがない場合もある。
        // 起動済み判定には、登録時のタイトルやMainWindowHandleを使わない。
        var processes = Process.GetProcessesByName(app.ProcessName);
        try
        {
            return processes.Length > 0;
        }
        finally
        {
            foreach (var process in processes) process.Dispose();
        }
    }

    private static Process? FindProcess(AppInfoForm app)
    {
        var processes = Process.GetProcessesByName(app.ProcessName);
        var result = string.IsNullOrWhiteSpace(app.WindowTitle)
            ? processes.FirstOrDefault(x => x.MainWindowHandle != IntPtr.Zero)
            : processes.FirstOrDefault(x =>
                x.MainWindowHandle != IntPtr.Zero &&
                string.Equals(x.MainWindowTitle, app.WindowTitle, StringComparison.Ordinal));
        foreach (var process in processes.Where(x => !ReferenceEquals(x, result))) process.Dispose();
        return result;
    }

    private static bool TryGetWindowInfo(AppInfoForm app, out AppInfoForm captured)
    {
        captured = app;
        using var process = FindProcess(app);
        if (process is null || !Win32Helper.TryGetRestoredWindowRect(process.MainWindowHandle, out var rect, out var isMinimized)) return false;
        captured.WindowTitle = process.MainWindowTitle;
        captured.X = rect.Left;
        captured.Y = rect.Top;
        captured.Width = rect.Right - rect.Left;
        captured.Height = rect.Bottom - rect.Top;
        captured.IsMinimized = isMinimized;
        try { captured.AppExePath = process.MainModule?.FileName ?? captured.AppExePath; } catch { }
        return true;
    }

    private static void Save(AppInfoForm app) => DAO_StreamWindow.Save(new T_StreamWindow
    {
        ProcessName = app.ProcessName,
        WindowTitle = app.WindowTitle,
        AppExePath = app.AppExePath,
        X = app.X ?? 0,
        Y = app.Y ?? 0,
        Width = app.Width ?? 0,
        Height = app.Height ?? 0,
        IsAutoStart = app.IsAutoStart,
        IsMinimized = app.IsMinimized,
        CreatedDateTime = DateTime.Now,
        UpdatedDateTime = DateTime.Now,
        LastUsedDateTime = DateTime.Now
    });

    private bool Start(AppInfoForm app)
    {
        try
        {
            // 自動・一括・個別のどの起動経路でも二重起動を防ぐ。
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
            Process.Start(new ProcessStartInfo
            {
                FileName = app.AppExePath,
                WorkingDirectory = Path.GetDirectoryName(app.AppExePath) ?? string.Empty,
                UseShellExecute = true
            });
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
        var moved = Win32Helper.SetAppWindowRect(app);
        ShowStatus(moved ? $"アプリを配置しました: {app.ProcessName}" : $"配置失敗: {app.ProcessName}", moved);
    }

    private void Stop(AppInfoForm app)
    {
        var processes = Process.GetProcessesByName(app.ProcessName);
        if (processes.Length == 0)
        {
            ShowStatus($"停止対象が見つかりません: {app.ProcessName}", false);
            return;
        }
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
                }
            }
            ShowStatus($"アプリを停止しました: {app.ProcessName}");
        }
        catch (Exception ex) { ShowStatus($"停止失敗: {ex.Message}", false); }
    }

    private void ShowStatus(string message, bool success = true)
    {
        if (MainWindow is null) return;
        MainWindow.StatusTextBlock.Text = message;
        MainWindow.StatusTextBlock.Foreground = success ? System.Windows.Media.Brushes.LightGreen : System.Windows.Media.Brushes.OrangeRed;
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
        using var process = FindProcess(app);
        if (process is null) Start(app); else Move(app);
    }

    private void SavePositionButton_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as Button)?.DataContext is not AppInfoForm app || !TryGetWindowInfo(app, out var captured))
        {
            ShowStatus("起動中の対象ウィンドウが見つかりません。", false);
            return;
        }
        Save(captured);
        ReloadRegisteredApps();
        ShowStatus($"位置を保存しました: {app.ProcessName}");
    }

    private void SetPathButton_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as Button)?.DataContext is not AppInfoForm app) return;
        var dialog = new OpenFileDialog
        {
            Filter = "起動可能なファイル (*.exe;*.bat;*.cmd)|*.exe;*.bat;*.cmd|実行ファイル (*.exe)|*.exe|バッチファイル (*.bat;*.cmd)|*.bat;*.cmd",
            Title = "起動するアプリまたはバッチファイルを選択"
        };
        if (File.Exists(app.AppExePath)) dialog.FileName = app.AppExePath;
        if (dialog.ShowDialog() != true) return;
        app.AppExePath = dialog.FileName;
        Save(app);
        ShowStatus($"起動ファイルを設定しました: {app.ProcessName}");
    }

    private void StopButton_Click(object sender, RoutedEventArgs e) { if ((sender as Button)?.DataContext is AppInfoForm app) Stop(app); }
    private void DeleteButton_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as Button)?.DataContext is not AppInfoForm app) return;
        DAO_StreamWindow.Delete(app.ProcessName);
        ReloadRegisteredApps();
        ShowStatus($"登録を削除しました: {app.ProcessName}");
    }

    private void StartAllButton_Click(object sender, RoutedEventArgs e) => StartRegisteredApps();
    private void MoveAllButton_Click(object sender, RoutedEventArgs e) { foreach (var app in RegisteredApps.Where(x => x.Status == "起動中")) Win32Helper.SetAppWindowRect(app); ShowStatus("登録済みアプリを一括配置しました。"); }
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

    private void AutoStartCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        if (isLoadingAutoStartSetting) return;
        DAO_Setting.InsertUpdate(
            DAO_Setting.SettingName.AutoStartRegisteredApps,
            (AutoStartCheckBox.IsChecked == true).ToString());
        ShowStatus(AutoStartCheckBox.IsChecked == true
            ? "登録済みアプリの自動起動を有効にしました。"
            : "登録済みアプリの自動起動を無効にしました。");
    }
}
