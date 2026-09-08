using JTSA.Forms;
using JTSA.Models;
using Microsoft.Win32;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Input;

namespace JTSA;

public partial class AppRegistrationWindow : Window
{
    public ObservableCollection<AppInfoForm> RunningApps { get; } = [];
    public AppInfoForm? SelectedApp { get; private set; }

    private readonly AppInfoForm? editingApp;

    public AppRegistrationWindow(AppInfoForm? existingApp = null)
    {
        editingApp = existingApp;
        InitializeComponent();
        DataContext = this;
        ReloadRunningApps();
        Loaded += (_, _) => ApplyEditingApp();
    }

    private void ApplyEditingApp()
    {
        if (editingApp is null) return;

        Title = "アプリ設定";
        ConfirmButton.Content = "保存";
        AppPathTextBox.Text = editingApp.AppExePath;
        WindowProcessNameTextBox.Text = editingApp.GetWindowProcessName();
        WindowTitleTextBox.Text = editingApp.WindowTitle;
        ListenPortTextBox.Text = editingApp.ListenPort > 0 ? editingApp.ListenPort.ToString() : "";
        TitleMatchModeComboBox.SelectedIndex = editingApp.WindowTitleMatchMode == WindowTitleMatchMode.Contains ? 1 : 0;
    }

    private void ReloadRunningApps()
    {
        RunningApps.Clear();
        foreach (var window in Win32Helper.ListTopLevelWindows())
        {
            RunningApps.Add(new AppInfoForm
            {
                ProcessName = window.ProcessName,
                WindowTitle = window.Title,
                AppExePath = window.AppExePath
            });
        }
    }

    private void RegisterButton_Click(object sender, RoutedEventArgs e) => ConfirmSelection();
    private void RunningAppListBox_MouseDoubleClick(object sender, MouseButtonEventArgs e) => ConfirmSelection();
    private void ReloadButton_Click(object sender, RoutedEventArgs e) => ReloadRunningApps();

    private void RunningAppListBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (RunningAppListBox.SelectedItem is not AppInfoForm app) return;
        WindowProcessNameTextBox.Text = app.ProcessName;
        WindowTitleTextBox.Text = app.WindowTitle;
        var existingPath = AppPathTextBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(existingPath) && !string.IsNullOrWhiteSpace(app.AppExePath))
        {
            AppPathTextBox.Text = app.AppExePath;
        }
    }

    private void BrowsePathButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Filter = "起動可能なファイル (*.exe;*.bat;*.cmd)|*.exe;*.bat;*.cmd|実行ファイル (*.exe)|*.exe|バッチファイル (*.bat;*.cmd)|*.bat;*.cmd",
            Title = "起動するアプリまたはバッチファイルを選択"
        };
        if (File.Exists(AppPathTextBox.Text)) dialog.FileName = AppPathTextBox.Text;
        if (dialog.ShowDialog() != true) return;
        AppPathTextBox.Text = dialog.FileName;
        if (RunningAppListBox.SelectedItem is AppInfoForm app
            && IsSeparateWindowProcess(dialog.FileName, app.ProcessName))
        {
            WindowProcessNameTextBox.Text = app.ProcessName;
            if (string.IsNullOrWhiteSpace(WindowTitleTextBox.Text))
            {
                WindowTitleTextBox.Text = app.WindowTitle;
            }
        }
    }

    private void ConfirmSelection()
    {
        var path = AppPathTextBox.Text.Trim();
        var windowProcessName = WindowProcessNameTextBox.Text.Trim();
        var windowTitle = WindowTitleTextBox.Text.Trim();
        if (!TryParseListenPort(ListenPortTextBox.Text, out var listenPort, out var portError))
        {
            MessageBox.Show(this, portError, Title, MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        var matchMode = TitleMatchModeComboBox.SelectedIndex == 1
            ? WindowTitleMatchMode.Contains
            : WindowTitleMatchMode.Exact;

        if (editingApp is not null)
        {
            if (!string.IsNullOrWhiteSpace(path) && !File.Exists(path))
            {
                MessageBox.Show(
                    this,
                    "指定した起動ファイルが見つかりません。",
                    "アプリ設定",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            var processName = editingApp.ProcessName;
            var appExePath = string.IsNullOrWhiteSpace(path) ? editingApp.AppExePath : path;
            SelectedApp = new AppInfoForm
            {
                ProcessName = processName,
                WindowTitle = windowTitle,
                AppExePath = appExePath,
                WindowProcessName = AppInfoForm.NormalizeWindowProcessName(windowProcessName, processName, appExePath),
                WindowTitleMatchMode = matchMode,
                ListenPort = listenPort,
                X = editingApp.X,
                Y = editingApp.Y,
                Width = editingApp.Width,
                Height = editingApp.Height,
                IsAutoStart = editingApp.IsAutoStart,
                IsMinimized = editingApp.IsMinimized
            };
            DialogResult = true;
            return;
        }

        if (RunningAppListBox.SelectedItem is AppInfoForm app)
        {
            if (!string.IsNullOrWhiteSpace(path)) app.AppExePath = path;
            if (!string.IsNullOrWhiteSpace(windowProcessName)) app.ProcessName = windowProcessName;
            app.WindowTitle = windowTitle;
            app.WindowTitleMatchMode = matchMode;
            app.ListenPort = listenPort;
            app.WindowProcessName = AppInfoForm.NormalizeWindowProcessName(
                windowProcessName, app.ProcessName, app.AppExePath);
            SelectedApp = app;
            DialogResult = true;
            return;
        }

        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            MessageBox.Show(
                this,
                "起動中アプリを選択するか、存在する起動ファイルの Path を指定してください。",
                "アプリを登録",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        var newProcessName = string.IsNullOrWhiteSpace(windowProcessName)
            ? Path.GetFileNameWithoutExtension(path) ?? ""
            : windowProcessName;
        SelectedApp = new AppInfoForm
        {
            ProcessName = newProcessName,
            WindowTitle = windowTitle,
            AppExePath = path,
            WindowTitleMatchMode = matchMode,
            ListenPort = listenPort,
            WindowProcessName = AppInfoForm.NormalizeWindowProcessName(windowProcessName, newProcessName, path)
        };
        DialogResult = true;
    }

    private static bool TryParseListenPort(string text, out int listenPort, out string error)
    {
        listenPort = 0;
        error = "";
        var trimmed = text.Trim();
        if (string.IsNullOrWhiteSpace(trimmed)) return true;
        if (!int.TryParse(trimmed, out listenPort) || listenPort is < 1 or > 65535)
        {
            error = "ポートは 1〜65535 の数値で指定してください。";
            listenPort = 0;
            return false;
        }
        return true;
    }

    private static bool IsSeparateWindowProcess(string launchPath, string windowProcessName)
    {
        if (string.IsNullOrWhiteSpace(launchPath) || string.IsNullOrWhiteSpace(windowProcessName)) return false;
        if (AppInfoForm.IsBatchPath(launchPath)) return true;
        var launchName = Path.GetFileNameWithoutExtension(launchPath);
        return !string.Equals(launchName, windowProcessName, StringComparison.OrdinalIgnoreCase);
    }
}
