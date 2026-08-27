using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace JTSA;

public partial class ToolPanelWindow : Window
{
    private const int DwmWindowCornerPreference = 33;
    private const int DwmWindowCornerDoNotRound = 1;

    public ToolPanelWindow(string title, FrameworkElement content)
    {
        InitializeComponent();
        Title = title;
        PanelContent.Content = content;
        SourceInitialized += ToolPanelWindow_SourceInitialized;
    }

    public FrameworkElement? ReleaseContent()
    {
        var content = PanelContent.Content as FrameworkElement;
        PanelContent.Content = null;
        return content;
    }

    private void ToolPanelWindow_SourceInitialized(object? sender, EventArgs e)
    {
        var preference = DwmWindowCornerDoNotRound;
        DwmSetWindowAttribute(new WindowInteropHelper(this).Handle,
            DwmWindowCornerPreference, ref preference, Marshal.SizeOf<int>());
    }

    private void MinimizeButton_Click(object sender, RoutedEventArgs e) =>
        WindowState = WindowState.Minimized;

    private void MaximizeButton_Click(object sender, RoutedEventArgs e) =>
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;

    private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(
        IntPtr windowHandle, int attribute, ref int attributeValue, int attributeSize);
}
