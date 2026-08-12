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
        if (response.StatusCode is not HttpStatusCode.Unauthorized and not HttpStatusCode.Forbidden)
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
                $"{operationName}に必要な権限がありません。再認証してください。\n必要な権限: {scopeText}",
                responseDetail);
        });
    }
}
