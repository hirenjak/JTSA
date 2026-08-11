using System.Net;
using System.Net.Http;
using System.Windows;

namespace JTSA.Utility;

internal static class TwitchPermissionNotifier
{
    private static readonly object syncRoot = new();
    private static readonly Dictionary<string, DateTime> lastNotifications = new();
    private static readonly TimeSpan notificationInterval = TimeSpan.FromMinutes(5);

    public static async Task NotifyIfRequiredAsync(
        HttpResponseMessage response,
        string operationName,
        params string[] requiredScopes)
    {
        if (response.StatusCode is not HttpStatusCode.Unauthorized and not HttpStatusCode.Forbidden)
        {
            return;
        }

        var key = $"{operationName}:{response.StatusCode}";
        lock (syncRoot)
        {
            if (lastNotifications.TryGetValue(key, out var lastNotification) &&
                DateTime.Now - lastNotification < notificationInterval)
            {
                return;
            }

            lastNotifications[key] = DateTime.Now;
        }

        var responseDetail = await response.Content.ReadAsStringAsync();
        var scopeText = requiredScopes.Length == 0
            ? "Twitch APIの必要権限"
            : string.Join("\n", requiredScopes.Select(scope => $"・{scope}"));

        var reason = response.StatusCode == HttpStatusCode.Unauthorized
            ? "アクセストークンが無効、期限切れ、または必要な権限が不足しています。"
            : "この操作に必要な権限がありません。";

        var message = $"{operationName}を実行できませんでした。\n\n" +
                      $"{reason}\n\n必要な権限:\n{scopeText}\n\n" +
                      "Settingタブの「OAuth再認証」から認証し直してください。";

        if (!string.IsNullOrWhiteSpace(responseDetail))
        {
            message += $"\n\nTwitchからの応答:\n{responseDetail}";
        }

        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher == null) return;

        await dispatcher.InvokeAsync(() => MessageBox.Show(
            message,
            "Twitch権限エラー",
            MessageBoxButton.OK,
            MessageBoxImage.Warning));
    }
}
