using JTSA.Forms;
using Microsoft.Win32;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace JTSA;

public partial class AppRegistrationWindow : Window
{
    public ObservableCollection<AppInfoForm> RunningApps { get; } = [];
    public AppInfoForm? SelectedApp { get; private set; }

    public AppRegistrationWindow()
    {
        InitializeComponent();
        DataContext = this;
        ReloadRunningApps();
    }

    private void ReloadRunningApps()
    {
        RunningApps.Clear();
        foreach (var process in Process.GetProcesses().OrderBy(item => item.ProcessName))
        {
            try
            {
                if (process.MainWindowHandle == IntPtr.Zero || string.IsNullOrWhiteSpace(process.MainWindowTitle)) continue;
                RunningApps.Add(new AppInfoForm
                {
                    ProcessName = process.ProcessName,
                    WindowTitle = process.MainWindowTitle,
                    AppExePath = TryGetProcessPath(process)
                });
            }
            catch { }
            finally { process.Dispose(); }
        }
    }

    private static string TryGetProcessPath(Process process)
    {
        try
        {
            return process.MainModule?.FileName ?? string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }

    private void RegisterButton_Click(object sender, RoutedEventArgs e) => ConfirmSelection();
    private void RunningAppListBox_MouseDoubleClick(object sender, MouseButtonEventArgs e) => ConfirmSelection();
    private void ReloadButton_Click(object sender, RoutedEventArgs e) => ReloadRunningApps();

    private void RunningAppListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (RunningAppListBox.SelectedItem is not AppInfoForm app) return;
        if (!string.IsNullOrWhiteSpace(app.AppExePath))
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
    }

    private void ConfirmSelection()
    {
        var path = AppPathTextBox.Text.Trim();
        if (RunningAppListBox.SelectedItem is AppInfoForm app)
        {
            if (!string.IsNullOrWhiteSpace(path)) app.AppExePath = path;
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

        SelectedApp = new AppInfoForm
        {
            ProcessName = Path.GetFileNameWithoutExtension(path),
            WindowTitle = string.Empty,
            AppExePath = path
        };
        DialogResult = true;
    }
}
