using Microsoft.Web.WebView2.Core;
using System.Windows;

namespace JTSA;

public partial class InAppBrowserWindow : Window
{
    private Uri currentUri;
    private readonly bool dockToOwnerLeft;

    public InAppBrowserWindow(Uri uri, bool dockToOwnerLeft = false)
    {
        currentUri = uri;
        this.dockToOwnerLeft = dockToOwnerLeft;
        InitializeComponent();
        if (dockToOwnerLeft)
        {
            WindowStartupLocation = WindowStartupLocation.Manual;
            MinWidth = 280;
            Width = 320;
        }
        Loaded += InAppBrowserWindow_Loaded;
        Closed += InAppBrowserWindow_Closed;
    }

    private async void InAppBrowserWindow_Loaded(object sender, RoutedEventArgs e)
    {
        try
        {
            if (dockToOwnerLeft && Owner is not null)
            {
                Owner.LocationChanged += Owner_PositionChanged;
                Owner.SizeChanged += Owner_PositionChanged;
                DockToOwnerLeft();
            }

            await Browser.EnsureCoreWebView2Async();
            Browser.CoreWebView2.NewWindowRequested += CoreWebView2_NewWindowRequested;
            Browser.CoreWebView2.NavigationCompleted += (_, _) => UpdateNavigationState();
            Browser.CoreWebView2.DocumentTitleChanged += (_, _) =>
                Title = string.IsNullOrWhiteSpace(Browser.CoreWebView2.DocumentTitle)
                    ? "ブラウザ"
                    : Browser.CoreWebView2.DocumentTitle;
            Browser.Source = currentUri;
            UpdateNavigationState();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"アプリ内ブラウザを開けませんでした。{ex.GetBaseException().Message}",
                "ブラウザ", MessageBoxButton.OK, MessageBoxImage.Warning);
            Close();
        }
    }

    private void Owner_PositionChanged(object? sender, EventArgs e) => DockToOwnerLeft();

    private void DockToOwnerLeft()
    {
        if (Owner is null || Owner.WindowState == WindowState.Minimized) return;

        Top = Owner.Top;
        Height = Math.Max(MinHeight, Owner.ActualHeight);
        // 両ウィンドウの1px枠を重ね、視覚上の隙間を作らない。
        Left = Owner.Left - ActualWidth + 1;
    }

    private void InAppBrowserWindow_Closed(object? sender, EventArgs e)
    {
        if (Owner is null) return;
        Owner.LocationChanged -= Owner_PositionChanged;
        Owner.SizeChanged -= Owner_PositionChanged;
    }

    private void CoreWebView2_NewWindowRequested(object? sender, CoreWebView2NewWindowRequestedEventArgs e)
    {
        e.Handled = true;
        Browser.CoreWebView2.Navigate(e.Uri);
    }

    private void UpdateNavigationState()
    {
        AddressTextBox.Text = Browser.Source?.AbsoluteUri ?? currentUri.AbsoluteUri;
        BackButton.IsEnabled = Browser.CanGoBack;
        ForwardButton.IsEnabled = Browser.CanGoForward;
    }

    private void BackButton_Click(object sender, RoutedEventArgs e)
    {
        if (Browser.CanGoBack) Browser.GoBack();
    }

    private void ForwardButton_Click(object sender, RoutedEventArgs e)
    {
        if (Browser.CanGoForward) Browser.GoForward();
    }

    private void ReloadButton_Click(object sender, RoutedEventArgs e) => Browser.Reload();

    public void NavigateTo(Uri uri)
    {
        currentUri = uri;
        if (Browser.CoreWebView2 is not null)
            Browser.CoreWebView2.Navigate(uri.AbsoluteUri);
    }
}
