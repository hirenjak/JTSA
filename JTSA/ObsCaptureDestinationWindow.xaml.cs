using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace JTSA;

public partial class ObsCaptureDestinationWindow : Window
{
    private const int DwmWindowCornerPreference = 33;
    private const int DwmWindowCornerDoNotRound = 1;

    public ObsCaptureDestinationWindow(string categoryId, string categoryName, string boxArtUrl)
    {
        InitializeComponent();
        Title = $"OBSキャプチャ先変更 - {categoryName}";
        CaptureSettingsPanel.ShowCaptureDestinationSettings(categoryId, categoryName, boxArtUrl);
        SourceInitialized += ObsCaptureDestinationWindow_SourceInitialized;
    }

    private void ObsCaptureDestinationWindow_SourceInitialized(object? sender, EventArgs e)
    {
        var preference = DwmWindowCornerDoNotRound;
        DwmSetWindowAttribute(new WindowInteropHelper(this).Handle, DwmWindowCornerPreference,
            ref preference, Marshal.SizeOf<int>());
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
