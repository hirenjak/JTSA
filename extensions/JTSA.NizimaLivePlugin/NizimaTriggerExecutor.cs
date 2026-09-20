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
        var modelId = rule.CommandType is NizimaTriggerCommands.AddModel or NizimaTriggerCommands.AddItem
            ? rule.ModelId
            : await client.ResolveModelIdAsync(rule.ModelId, cancellationToken).ConfigureAwait(false);

        switch (rule.CommandType)
        {
            case NizimaTriggerCommands.ChangeModel:
                await client.ChangeModelAsync(
                    modelId,
                    FirstNonEmpty(extra.ModelPath, rule.CommandValue),
                    cancellationToken).ConfigureAwait(false);
                break;
            case NizimaTriggerCommands.AddModel:
                var addModel = new JsonObject
                {
                    ["ModelPath"] = FirstNonEmpty(extra.ModelPath, rule.CommandValue)
                };
                if (!string.IsNullOrWhiteSpace(rule.SceneId))
                    addModel["SceneId"] = rule.SceneId;
                await client.SendRequestAsync("AddModel", addModel, cancellationToken)
                    .ConfigureAwait(false);
                break;
            case NizimaTriggerCommands.TriggerHotkey:
                await client.SendRequestAsync(
                    "TriggerModelHotkey",
                    new JsonObject { ["ModelId"] = modelId, ["Key"] = rule.CommandValue },
                    cancellationToken).ConfigureAwait(false);
                break;
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
            case NizimaTriggerCommands.AddItem:
                if (string.IsNullOrWhiteSpace(rule.SceneId))
                    throw new InvalidOperationException("AddItem には SceneId が必要です。");
                await client.SendRequestAsync(
                    "AddItem",
                    new JsonObject
                    {
                        ["SceneId"] = rule.SceneId,
                        ["ItemPath"] = FirstNonEmpty(extra.ItemPath, rule.CommandValue)
                    },
                    cancellationToken).ConfigureAwait(false);
                break;
            case NizimaTriggerCommands.RemoveItem:
                await client.SendRequestAsync(
                    "RemoveItem",
                    new JsonObject { ["ItemId"] = rule.CommandValue },
                    cancellationToken).ConfigureAwait(false);
                break;
            case NizimaTriggerCommands.MoveModel:
                await client.SendRequestAsync(
                    "MoveModel",
                    new JsonObject
                    {
                        ["ModelId"] = modelId,
                        ["Absolute"] = !extra.Relative,
                        ["PositionX"] = extra.X,
                        ["PositionY"] = extra.Y,
                        ["Rotation"] = extra.Rotation,
                        ["Scale"] = extra.Size,
                        ["Delay"] = extra.DelayMs / 1000.0
                    },
                    cancellationToken).ConfigureAwait(false);
                break;
            case NizimaTriggerCommands.SetModelColor:
                JsonObject color = extra.UseScreen
                    ? new JsonObject
                    {
                        ["Red"] = extra.R,
                        ["Green"] = extra.G,
                        ["Blue"] = extra.B,
                        ["Alpha"] = extra.A
                    }
                    : new JsonObject
                    {
                        ["Red"] = extra.R,
                        ["Green"] = extra.G,
                        ["Blue"] = extra.B,
                        ["Alpha"] = extra.A
                    };
                var payload = new JsonObject { ["ModelId"] = modelId };
                if (extra.UseScreen)
                    payload["ScreenColor"] = color;
                else
                    payload["MultiplyColor"] = color;
                await client.SendRequestAsync("SetModelColor", payload, cancellationToken)
                    .ConfigureAwait(false);
                break;
            case NizimaTriggerCommands.EffectOn:
                await client.SendRequestAsync(
                    "EnableEffectGroup",
                    new JsonObject { ["GroupId"] = rule.CommandValue },
                    cancellationToken).ConfigureAwait(false);
                break;
            case NizimaTriggerCommands.EffectOff:
                await client.SendRequestAsync(
                    "DisableEffectGroup",
                    new JsonObject { ["GroupId"] = rule.CommandValue },
                    cancellationToken).ConfigureAwait(false);
                break;
            case NizimaTriggerCommands.RawJson:
                await client.SendRawJsonAsync(extra.RawJson, cancellationToken).ConfigureAwait(false);
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
