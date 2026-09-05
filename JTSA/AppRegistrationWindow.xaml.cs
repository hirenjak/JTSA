using JTSA.Forms;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows;
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
                RunningApps.Add(new AppInfoForm { ProcessName = process.ProcessName, WindowTitle = process.MainWindowTitle });
            }
            catch { }
            finally { process.Dispose(); }
        }
    }

    private void RegisterButton_Click(object sender, RoutedEventArgs e) => ConfirmSelection();
    private void RunningAppListBox_MouseDoubleClick(object sender, MouseButtonEventArgs e) => ConfirmSelection();
    private void ReloadButton_Click(object sender, RoutedEventArgs e) => ReloadRunningApps();

    private void ConfirmSelection()
    {
        if (RunningAppListBox.SelectedItem is not AppInfoForm app) return;
        SelectedApp = app;
        DialogResult = true;
    }
}
