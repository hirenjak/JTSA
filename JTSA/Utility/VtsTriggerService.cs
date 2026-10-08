using System.Globalization;
using System.Windows;

namespace JTSA.Utility;

internal static class VtsTriggerService
{
    public static async Task HandleAsync(StreamExpansionTriggerType type, string value)
    {
        try
        {
            var rules = VtsTriggerStore.Load()
                .Where(rule => VtsTriggerMatcher.Matches(rule, type, value))
                .ToList();
            if (rules.Count == 0)
                return;

            var client = await GetClientAsync();
            if (client is null || !client.IsAuthenticated)
            {
                LogError("VTS未接続のためトリガーをスキップしました。");
                return;
            }

            foreach (var rule in rules)
            {
                try
                {
                    await ExecuteAsync(client, rule);
                    LogSuccess($"VTSトリガー実行：{rule.TriggerType} → {rule.CommandType}");
                }
                catch (Exception ex)
                {
                    LogError($"VTSトリガー実行失敗（{rule.CommandType}）：{ex.Message}");
                }
            }
        }
        catch (Exception ex)
        {
            LogError($"VTSトリガー処理失敗（{type}）：{ex.Message}");
        }
    }

    internal static async Task ExecuteAsync(VtsClient client, VtsTriggerRule rule)
    {
        var extra = rule.Extra ?? new VtsTriggerCommandExtra();
        switch (rule.CommandType)
        {
            case VtsTriggerCommands.LoadModel:
                await client.LoadModelAsync(rule.CommandValue);
                break;
            case VtsTriggerCommands.TriggerHotkey:
                await client.TriggerHotkeyAsync(rule.CommandValue);
                break;
            case VtsTriggerCommands.ExpressionOn:
                await client.SetExpressionAsync(rule.CommandValue, true);
                break;
            case VtsTriggerCommands.ExpressionOff:
                await client.SetExpressionAsync(rule.CommandValue, false);
                break;
            case VtsTriggerCommands.LoadItem:
                await client.LoadItemAsync(rule.CommandValue);
                break;
            case VtsTriggerCommands.UnloadItem:
                await client.UnloadItemByFileNameAsync(rule.CommandValue);
                break;
            case VtsTriggerCommands.MoveModel:
                await client.MoveModelAsync(
                    extra.Time, extra.Relative, extra.X, extra.Y, extra.Rotation, extra.Size);
                break;
            case VtsTriggerCommands.Tint:
                await client.TintArtMeshesAsync(
                    extra.R, extra.G, extra.B, extra.A,
                    extra.TintAll || string.IsNullOrWhiteSpace(rule.CommandValue)
                        ? null
                        : [rule.CommandValue],
                    extra.TintAll || string.IsNullOrWhiteSpace(rule.CommandValue));
                break;
            case VtsTriggerCommands.PostProcessing:
                object[]? values = string.IsNullOrWhiteSpace(rule.CommandValue)
                    ? null
                    :
                    [
                        new
                        {
                            configID = rule.CommandValue,
                            configValue = extra.PostProcessingValue
                        }
                    ];
                await client.UpdatePostProcessingAsync(false, extra.PostProcessingOn, values);
                break;
            case VtsTriggerCommands.RawJson:
                await client.SendRawJsonAsync(extra.RawJson);
                break;
            default:
                throw new InvalidOperationException($"未知のVTSコマンドです: {rule.CommandType}");
        }
    }

    internal static async Task ExecuteHotkeyAsync(string hotkeyId)
    {
        if (string.IsNullOrWhiteSpace(hotkeyId))
            throw new ArgumentException("実行するVTSホットキーを選択してください。", nameof(hotkeyId));
        var client = await GetClientAsync();
        if (client is null || !client.IsAuthenticated)
            throw new InvalidOperationException("VTSに接続されていません。");
        await client.TriggerHotkeyAsync(hotkeyId);
    }

    private static Task<VtsClient?> GetClientAsync()
    {
        var application = Application.Current;
        if (application?.Dispatcher == null)
            return Task.FromResult<VtsClient?>(null);

        if (application.Dispatcher.CheckAccess())
            return Task.FromResult((application.MainWindow as MainWindow)?.VtsClient);

        return application.Dispatcher.InvokeAsync(
            () => (application.MainWindow as MainWindow)?.VtsClient).Task;
    }

    private static void LogSuccess(string message)
    {
        var application = Application.Current;
        if (application?.Dispatcher == null) return;
        if (!application.Dispatcher.CheckAccess())
        {
            application.Dispatcher.Invoke(() => LogSuccess(message));
            return;
        }

        if (application.MainWindow is MainWindow mainWindow)
            mainWindow.AppLogPanel.Success(nameof(VtsTriggerService), message);
    }

    private static void LogError(string message)
    {
        var application = Application.Current;
        if (application?.Dispatcher == null) return;
        if (!application.Dispatcher.CheckAccess())
        {
            application.Dispatcher.Invoke(() => LogError(message));
            return;
        }

        if (application.MainWindow is MainWindow mainWindow)
            mainWindow.AppLogPanel.Error(nameof(VtsTriggerService), message);
    }

    internal static IReadOnlyList<(string Id, string Name)> ScheduledTimes()
    {
        var times = new List<(string, string)>();
        for (var hour = 0; hour < 24; hour++)
        {
            for (var minute = 0; minute < 60; minute += 5)
            {
                var value = string.Create(CultureInfo.InvariantCulture, $"{hour:00}:{minute:00}");
                times.Add((value, value));
            }
        }

        return times;
    }
}
