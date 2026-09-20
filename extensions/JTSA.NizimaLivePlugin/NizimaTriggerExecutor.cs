using System.Text.Json.Nodes;
using JTSA.Plugin.Abstractions;

namespace JTSA.NizimaLivePlugin;

public static class NizimaTriggerExecutor
{
    public static async Task ExecuteAsync(
        NizimaClient client,
        NizimaTriggerRule rule,
        CancellationToken cancellationToken)
    {
        var extra = rule.Extra ?? new NizimaTriggerCommandExtra();
        var modelId = await client.ResolveModelIdAsync(rule.ModelId, cancellationToken).ConfigureAwait(false);

        switch (rule.CommandType)
        {
            case NizimaTriggerCommands.ChangeModel:
            {
                var modelPath = FirstNonEmpty(extra.ModelPath, rule.CommandValue);
                await client.ChangeModelAsync(modelId, modelPath, cancellationToken).ConfigureAwait(false);
                break;
            }
            case NizimaTriggerCommands.ExpressionOn:
                await client.SendRequestAsync(
                    "StartExpression",
                    new JsonObject { ["ModelId"] = modelId, ["ExpressionPath"] = rule.CommandValue },
                    cancellationToken).ConfigureAwait(false);
                break;
            case NizimaTriggerCommands.ExpressionOff:
                await client.SendRequestAsync(
                    "StopExpression",
                    new JsonObject { ["ModelId"] = modelId, ["ExpressionPath"] = rule.CommandValue },
                    cancellationToken).ConfigureAwait(false);
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
        ExpansionTriggerInfo info,
        Action<string> log,
        Action<string, Exception?> logError)
    {
        try
        {
            var matched = rules.Where(rule => NizimaTriggerMatcher.Matches(rule, info.TriggerType, info.Value))
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
                    await ExecuteAsync(client, rule, CancellationToken.None).ConfigureAwait(false);
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
            logError($"nizima トリガー処理失敗（{info.TriggerType}）", ex);
        }
    }

    private static string FirstNonEmpty(params string[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? "";
}
