using System.Windows;

namespace JTSA.Utility;

internal static class InAppBrowser
{
    private static readonly Dictionary<string, InAppBrowserWindow> OpenWindows =
        new(StringComparer.OrdinalIgnoreCase);

    public static void Open(string url, Window? owner = null, bool dockToOwnerLeft = false)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            throw new ArgumentException("HTTP または HTTPS のURLを指定してください。", nameof(url));
        }

        var windowKey = GetWindowKey(uri);
        if (OpenWindows.TryGetValue(windowKey, out var existingWindow))
        {
            existingWindow.NavigateTo(uri);
            if (existingWindow.WindowState == WindowState.Minimized)
                existingWindow.WindowState = WindowState.Normal;
            existingWindow.Show();
            existingWindow.Activate();
            return;
        }

        var window = new InAppBrowserWindow(uri, dockToOwnerLeft)
        {
            Owner = owner ?? Application.Current?.MainWindow
        };
        OpenWindows[windowKey] = window;
        window.Closed += (_, _) => OpenWindows.Remove(windowKey);
        window.Show();
        window.Activate();
    }

    private static string GetWindowKey(Uri uri)
    {
        var keyUri = new UriBuilder(uri) { Query = string.Empty, Fragment = string.Empty };
        return keyUri.Uri.AbsoluteUri.TrimEnd('/');
    }
}
