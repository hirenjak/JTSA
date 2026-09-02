using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;

namespace JTSA.Controls;

public partial class CategoryStyleTitleBar : UserControl
{
    private const int DwmWindowCornerPreference = 33;
    private const int DwmWindowCornerDoNotRound = 1;

    public CategoryStyleTitleBar()
    {
        InitializeComponent();
        Loaded += CategoryStyleTitleBar_Loaded;
    }

    private void CategoryStyleTitleBar_Loaded(object sender, RoutedEventArgs e)
    {
        var window = Window.GetWindow(this);
        if (window is null) return;
        MaximizeButton.Visibility = window.ResizeMode == ResizeMode.NoResize
            ? Visibility.Collapsed
            : Visibility.Visible;
        var preference = DwmWindowCornerDoNotRound;
        DwmSetWindowAttribute(new WindowInteropHelper(window).Handle, DwmWindowCornerPreference,
            ref preference, Marshal.SizeOf<int>());
    }

    private void MinimizeButton_Click(object sender, RoutedEventArgs e)
    {
        if (Window.GetWindow(this) is { } window) window.WindowState = WindowState.Minimized;
    }

    private void MaximizeButton_Click(object sender, RoutedEventArgs e)
    {
        if (Window.GetWindow(this) is not { } window) return;
        window.WindowState = window.WindowState == WindowState.Maximized
            ? WindowState.Normal
            : WindowState.Maximized;
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e) => Window.GetWindow(this)?.Close();

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(
        IntPtr windowHandle, int attribute, ref int attributeValue, int attributeSize);
}
