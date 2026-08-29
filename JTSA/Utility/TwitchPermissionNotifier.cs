using System.Net;
using System.Net.Http;
using System.Windows;

namespace JTSA.Utility;

internal static class TwitchPermissionNotifier
{
    public static async Task NotifyIfRequiredAsync(
        HttpResponseMessage response,
        string operationName,
        params string[] requiredScopes)
    {
        // A 403 response can describe an operation-specific restriction even when
        // the access token is still valid. Clearing the token in that case forces
        // an unnecessary OAuth flow, so only a 401 is treated as an authentication
        // failure here.
        if (response.StatusCode is not HttpStatusCode.Unauthorized)
        {
            return;
        }

        var responseDetail = await response.Content.ReadAsStringAsync();
        var application = Application.Current;
        if (application?.Dispatcher == null) return;

        await application.Dispatcher.InvokeAsync(() =>
        {
            if (application.MainWindow is not MainWindow mainWindow) return;

            var scopeText = requiredScopes.Length == 0
                ? "必要なTwitch API権限"
                : string.Join(", ", requiredScopes);

            mainWindow.RequireOAuthReauthentication(
                $"{operationName}の認証が無効です。再認証してください。\n必要な権限: {scopeText}",
                responseDetail);
        });
    }
}
