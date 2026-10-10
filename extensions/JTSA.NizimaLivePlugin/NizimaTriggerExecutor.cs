using System.Text.Json.Nodes;
using JTSA.Plugin.Abstractions;

namespace JTSA.NizimaLivePlugin;

public static class NizimaTriggerExecutor
{
    private static readonly Dictionary<string, CancellationTokenSource> autoOffTimers =
        new(StringComparer.OrdinalIgnoreCase);
    private static readonly object autoOffLock = new();

    public static async Task ExecuteAsync(
        NizimaClient client,
        NizimaTriggerRule rule,
        CancellationToken cancellationToken,
        Action<string, Exception?>? logError = null)
    {
        var extra = rule.Extra ?? new NizimaTriggerCommandExtra();
        var isItemExpression = NizimaCommandUi.ShowsItemTarget(rule.CommandType);
        if (isItemExpression && string.IsNullOrWhiteSpace(rule.ModelId))
            throw new InvalidOperationException("アイテムの表情には対象アイテムが必要です。");
        var modelId = isItemExpression
            ? rule.ModelId
            : await client.ResolveModelIdAsync(rule.ModelId, cancellationToken).ConfigureAwait(false);

        switch (rule.CommandType)
        {
            case NizimaTriggerCommands.ChangeModel:
            {
                var modelPath = FirstNonEmpty(extra.ModelPath, rule.CommandValue);
                await client.ChangeModelAsync(modelId, modelPath, cancellationToken).ConfigureAwait(false);
                break;
            }
            case NizimaTriggerCommands.ExpressionOn:
                await SetExpressionAsync(client, modelId, rule.CommandValue, active: true, cancellationToken)
                    .ConfigureAwait(false);
                ScheduleAutoOff(client, rule, modelId, logError);
                break;
            case NizimaTriggerCommands.ExpressionOff:
                await SetExpressionAsync(client, modelId, rule.CommandValue, active: false, cancellationToken)
                    .ConfigureAwait(false);
                break;
            case NizimaTriggerCommands.ExpressionToggle:
                await ToggleExpressionAsync(client, modelId, rule.CommandValue, cancellationToken)
                    .ConfigureAwait(false);
                break;
            case NizimaTriggerCommands.ItemExpressionOn:
                await SetExpressionAsync(client, modelId, rule.CommandValue, active: true, cancellationToken)
                    .ConfigureAwait(false);
                ScheduleAutoOff(client, rule, modelId, logError);
                break;
            case NizimaTriggerCommands.ItemExpressionOff:
                await SetExpressionAsync(client, modelId, rule.CommandValue, active: false, cancellationToken)
                    .ConfigureAwait(false);
                break;
            case NizimaTriggerCommands.ItemExpressionToggle:
                await ToggleExpressionAsync(client, modelId, rule.CommandValue, cancellationToken)
                    .ConfigureAwait(false);
                break;
            case NizimaTriggerCommands.StartMotion:
                await client.SendRequestAsync(
                    "StartMotion",
                    new JsonObject { ["ModelId"] = modelId, ["MotionPath"] = rule.CommandValue },
                    cancellationToken).ConfigureAwait(false);
                break;
            case NizimaTriggerCommands.StopMotion:
                await client.SendRequestAsync(
                    "StopMotion",
                    new JsonObject { ["ModelId"] = modelId, ["MotionPath"] = rule.CommandValue },
                    cancellationToken).ConfigureAwait(false);
                break;
            default:
                throw new InvalidOperationException($"未知のコマンドです: {rule.CommandType}");
        }
    }

    public static async Task HandleTriggerAsync(
        NizimaClient client,
        IReadOnlyList<NizimaTriggerRule> rules,
        string triggerType,
        string value,
        Action<string> log,
        Action<string, Exception?> logError)
    {
        try
        {
            var matched = rules.Where(rule => NizimaTriggerMatcher.Matches(rule, triggerType, value))
                .ToList();
            if (matched.Count == 0)
                return;
            if (!client.CanSendMethods)
            {
                logError("nizima LIVE 未有効のためトリガーをスキップしました。", null);
                return;
            }

            foreach (var rule in matched)
            {
                try
                {
                    await ExecuteAsync(client, rule, CancellationToken.None, logError).ConfigureAwait(false);
                    log($"nizima トリガー実行：{rule.TriggerType} → {rule.CommandType}");
                }
                catch (Exception ex)
                {
                    logError($"nizima トリガー実行失敗（{rule.CommandType}）", ex);
                }
            }
        }
        catch (Exception ex)
        {
            logError($"nizima トリガー処理失敗（{triggerType}）", ex);
        }
    }

    public static void CancelAllAutoOff()
    {
        lock (autoOffLock)
        {
            foreach (var cts in autoOffTimers.Values)
            {
                cts.Cancel();
                cts.Dispose();
            }
            autoOffTimers.Clear();
        }
    }

    private static void ScheduleAutoOff(
        NizimaClient client,
        NizimaTriggerRule rule,
        string modelId,
        Action<string, Exception?>? logError)
    {
        if (rule.AutoOffValue <= 0)
            return;

        // 同じモデルと表情の組み合わせで再度オンになったらタイマーを最初から数え直す。
        var key = $"{modelId}\u001f{rule.CommandValue}";
        var cts = new CancellationTokenSource();
        lock (autoOffLock)
        {
            if (autoOffTimers.Remove(key, out var previous))
            {
                previous.Cancel();
                previous.Dispose();
            }
            autoOffTimers[key] = cts;
        }

        var delay = NizimaAutoOffUnits.ToTimeSpan(rule.AutoOffValue, rule.AutoOffUnit);
        _ = RunAutoOffAsync(client, modelId, rule.CommandValue, delay, key, cts, logError);
    }

    private static async Task RunAutoOffAsync(
        NizimaClient client,
        string modelId,
        string expressionPath,
        TimeSpan delay,
        string key,
        CancellationTokenSource cts,
        Action<string, Exception?>? logError)
    {
        try
        {
            await Task.Delay(delay, cts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        lock (autoOffLock)
        {
            if (!autoOffTimers.TryGetValue(key, out var current) || current != cts)
                return;
            autoOffTimers.Remove(key);
        }
        cts.Dispose();

        if (!client.CanSendMethods)
        {
            logError?.Invoke($"nizima LIVE 未接続のため表情の自動解除をスキップしました（{expressionPath}）。", null);
            return;
        }

        try
        {
            await SetExpressionAsync(client, modelId, expressionPath, active: false, CancellationToken.None)
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logError?.Invoke($"nizima 表情の自動解除失敗（{expressionPath}）", ex);
        }
    }

    private static async Task ToggleExpressionAsync(
        NizimaClient client,
        string modelId,
        string expressionPath,
        CancellationToken cancellationToken)
    {
        var active = await IsExpressionActiveAsync(client, modelId, expressionPath, cancellationToken)
            .ConfigureAwait(false);
        await SetExpressionAsync(client, modelId, expressionPath, !active, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<bool> IsExpressionActiveAsync(
        NizimaClient client,
        string modelId,
        string expressionPath,
        CancellationToken cancellationToken)
    {
        var data = await client.SendRequestAsync(
            "GetExpressions",
            new JsonObject { ["ModelId"] = modelId },
            cancellationToken).ConfigureAwait(false);
        if (!data.TryGetProperty("Expressions", out var array) ||
            array.ValueKind != System.Text.Json.JsonValueKind.Array)
            return false;

        foreach (var item in array.EnumerateArray())
        {
            var path = item.TryGetProperty("ExpressionPath", out var pathNode) ? pathNode.GetString() : null;
            if (!string.Equals(path, expressionPath, StringComparison.OrdinalIgnoreCase))
                continue;
            return item.TryGetProperty("Active", out var activeNode) &&
                   activeNode.ValueKind == System.Text.Json.JsonValueKind.True;
        }

        return false;
    }

    private static Task SetExpressionAsync(
        NizimaClient client,
        string modelId,
        string expressionPath,
        bool active,
        CancellationToken cancellationToken) =>
        client.SendRequestAsync(
            active ? "StartExpression" : "StopExpression",
            new JsonObject { ["ModelId"] = modelId, ["ExpressionPath"] = expressionPath },
            cancellationToken);

    private static string FirstNonEmpty(params string[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? "";
}
