using JTSA.Plugin.Abstractions;
using JTSA.Panels;
using System.IO;
using System.Windows;
using System.Windows.Interop;

namespace JTSA.ExternalAppsPlugin;

public partial class ExternalAppsWindow : Window
{
    public ExternalAppsWindow(IJtsaPluginContext context)
    {
        InitializeComponent();
        PanelHost.Content = new AppArrangePanel(
            Path.Combine(context.DataDirectory, "external-apps-settings.json"),
            (message, success) =>
            {
                if (success) context.Log(message);
                else context.LogError(message);
            });
        if (context.MainWindowHandle != nint.Zero)
            new WindowInteropHelper(this) { Owner = context.MainWindowHandle };
    }

    private void MinimizeButton_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;
    private void MaximizeButton_Click(object sender, RoutedEventArgs e) =>
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
    private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();
    private void Window_StateChanged(object? sender, EventArgs e) =>
        MaximizeButton.Content = WindowState == WindowState.Maximized ? "\uE923" : "\uE922";
}
