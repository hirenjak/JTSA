using System.Text.Json;
using System.Windows.Input;
using JTSA.Plugin.Abstractions;
using Xunit;

namespace JTSA.NizimaLivePlugin.Tests;

public class NizimaProtocolTests
{
    [Fact]
    public void CreateRequestUsesEnvelope()
    {
        var json = NizimaProtocol.CreateRequest("ChangeModel").ToJsonString();
        using var doc = JsonDocument.Parse(json);
        Assert.Equal(NizimaProtocol.ApiVersion, doc.RootElement.GetProperty("nLPlugin").GetString());
        Assert.Equal("Request", doc.RootElement.GetProperty("Type").GetString());
        Assert.Equal("ChangeModel", doc.RootElement.GetProperty("Method").GetString());
    }

    [Fact]
    public void InvalidTokenRequiresReregister()
    {
        Assert.True(NizimaProtocol.ShouldReregister("InvalidToken"));
        Assert.False(NizimaProtocol.ShouldReregister("Other"));
    }

    [Fact]
    public void LoopbackDetection()
    {
        Assert.False(NizimaProtocol.IsNonLoopbackUrl("ws://127.0.0.1:22022/"));
        Assert.False(NizimaProtocol.IsNonLoopbackUrl("ws://localhost:22022/"));
        Assert.True(NizimaProtocol.IsNonLoopbackUrl("ws://192.168.0.2:22022/"));
    }
}

public class NizimaEnabledGateTests
{
    [Fact]
    public void DisabledClientBlocksMethods()
    {
        using var client = new NizimaClient();
        client.ApplyEnabledForTests(false);
        Assert.False(client.TryPeekWouldSend("StartExpression"));
        Assert.True(client.TryPeekWouldSend("RegisterPlugin"));
        Assert.True(client.TryPeekWouldSend("EstablishConnection"));
    }

    [Fact]
    public void PluginDisabledEventBlocksMethods()
    {
        using var client = new NizimaClient();
        client.ApplyEnabledForTests(true);
        client.HandleMessage("""{"nLPlugin":"1.0.0","Type":"Error","Method":"StartExpression","Data":{"ErrorType":"PluginDisabled"}}""");
        Assert.False(client.IsEnabled);
        Assert.False(client.TryPeekWouldSend("ChangeModel"));
    }
}

public class NizimaTriggerMatcherTests
{
    [Fact]
    public void DisabledRuleDoesNotMatch()
    {
        var rule = Rule(NizimaTriggerTypes.Chat, "hello", enabled: false);
        Assert.False(NizimaTriggerMatcher.Matches(rule, NizimaTriggerTypes.Chat, "hello world"));
    }

    [Fact]
    public void ChannelPointMatchesIdCaseInsensitive()
    {
        var rule = Rule(NizimaTriggerTypes.ChannelPoint, "abc-1");
        Assert.True(NizimaTriggerMatcher.Matches(rule, NizimaTriggerTypes.ChannelPoint, "ABC-1"));
        Assert.False(NizimaTriggerMatcher.Matches(rule, NizimaTriggerTypes.ChannelPoint, "other"));
        Assert.False(NizimaTriggerMatcher.Matches(rule, NizimaTriggerTypes.Chat, "abc-1"));
    }

    [Fact]
    public void ChatMatchesSubstringIgnoreCase()
    {
        var rule = Rule(NizimaTriggerTypes.Chat, "にゃん");
        Assert.True(NizimaTriggerMatcher.Matches(rule, NizimaTriggerTypes.Chat, "こんにちはにゃんこ"));
        Assert.False(NizimaTriggerMatcher.Matches(rule, NizimaTriggerTypes.Chat, "こんにちは"));
    }

    [Fact]
    public void ScheduledTimeMatchesExactValue()
    {
        var rule = Rule(NizimaTriggerTypes.ScheduledTime, "21:05");
        Assert.True(NizimaTriggerMatcher.Matches(rule, NizimaTriggerTypes.ScheduledTime, "21:05"));
        Assert.False(NizimaTriggerMatcher.Matches(rule, NizimaTriggerTypes.ScheduledTime, "21:00"));
    }

    [Fact]
    public void ObsStreamStartMatchesMainOrSub()
    {
        var main = Rule(NizimaTriggerTypes.ObsStreamStart, "main");
        Assert.True(NizimaTriggerMatcher.Matches(main, NizimaTriggerTypes.ObsStreamStart, "main"));
        Assert.False(NizimaTriggerMatcher.Matches(main, NizimaTriggerTypes.ObsStreamStart, "sub"));
    }

    private static NizimaTriggerRule Rule(string type, string value, bool enabled = true) => new()
    {
        IsEnabled = enabled,
        TriggerType = type,
        TriggerValue = value
    };
}

public class NizimaAutoConnectRetryTests
{
    [Fact]
    public void NextDelayDoublesUntilCap()
    {
        var first = NizimaAutoConnectRetry.NextDelay(NizimaAutoConnectRetry.FirstRetryDelay);
        Assert.Equal(TimeSpan.FromSeconds(10), first);
        var second = NizimaAutoConnectRetry.NextDelay(first);
        Assert.Equal(TimeSpan.FromSeconds(20), second);
        var third = NizimaAutoConnectRetry.NextDelay(second);
        Assert.Equal(NizimaAutoConnectRetry.MaxRetryDelay, third);
    }
}

public class NizimaChangeModelTests
{
    [Fact]
    public void ChangeModelPayloadKeepsModelIdAndPath()
    {
        var payload = NizimaChangeModelRequest.Create("model-1", @"C:\models\a.model3.json");
        var json = System.Text.Json.JsonSerializer.Serialize(payload);
        using var doc = JsonDocument.Parse(json);
        Assert.Equal("model-1", doc.RootElement.GetProperty("ModelId").GetString());
        Assert.Contains("a.model3.json", doc.RootElement.GetProperty("ModelPath").GetString());
    }
}

public class NizimaNamedOptionTests
{
    [Fact]
    public void FromArrayReadsNameAndPath()
    {
        using var doc = JsonDocument.Parse("""
            {"Expressions":[{"Name":"笑顔","ExpressionPath":"exp/smile.exp3.json"}]}
            """);
        var list = NizimaNamedOption.FromArray(doc.RootElement, "Expressions", "Name", "ExpressionPath");
        Assert.Single(list);
        Assert.Equal("笑顔", list[0].Name);
        Assert.Equal("exp/smile.exp3.json", list[0].Path);
        Assert.Equal("笑顔", list[0].Label);
    }
}

public class NizimaChoiceLabelTests
{
    [Fact]
    public void TriggerAndCommandUseJapaneseLabels()
    {
        Assert.Equal("チャンネルポイント", NizimaTriggerTypes.LabelOf(NizimaTriggerTypes.ChannelPoint));
        Assert.Equal("表情をオン", NizimaTriggerCommands.LabelOf(NizimaTriggerCommands.ExpressionOn));
        Assert.Equal("unknown", NizimaTriggerTypes.LabelOf("unknown"));
    }
}

public class NizimaTriggerRuleSummaryTests
{
    [Fact]
    public void IncludesCommandValueAndOmitsEmptyModel()
    {
        var rule = new NizimaTriggerRule
        {
            TriggerType = NizimaTriggerTypes.ChannelPoint,
            TriggerValue = "reward-1",
            CommandType = NizimaTriggerCommands.ExpressionOn,
            CommandValue = "exp/smile.exp3.json"
        };
        var expressions = new[] { new NizimaNamedOption("笑顔", "exp/smile.exp3.json") };
        var text = NizimaTriggerRuleSummary.Format(rule, new NizimaRuleCatalogs { Expressions = expressions });
        Assert.Equal("ON チャンネルポイント:reward-1 → 表情をオン 笑顔", text);
        Assert.DoesNotContain("model=", text);
    }

    [Fact]
    public void ShowsAutoOffOnlyForSupportedCommands()
    {
        var rule = new NizimaTriggerRule
        {
            TriggerType = NizimaTriggerTypes.Follow,
            CommandType = NizimaTriggerCommands.ExpressionOn,
            CommandValue = "smile",
            AutoOffValue = 10,
            AutoOffUnit = NizimaAutoOffUnits.Minutes
        };
        Assert.Equal("ON フォロー → 表情をオン smile（10分後に解除）", NizimaTriggerRuleSummary.Format(rule));

        rule.CommandType = NizimaTriggerCommands.ExpressionToggle;
        Assert.DoesNotContain("解除", NizimaTriggerRuleSummary.Format(rule));
    }

    [Fact]
    public void AutoOffUnitsConvertToTimeSpan()
    {
        Assert.Equal(TimeSpan.FromSeconds(30), NizimaAutoOffUnits.ToTimeSpan(30, NizimaAutoOffUnits.Seconds));
        Assert.Equal(TimeSpan.FromMinutes(5), NizimaAutoOffUnits.ToTimeSpan(5, NizimaAutoOffUnits.Minutes));
        Assert.Equal(NizimaAutoOffUnits.Max, NizimaAutoOffUnits.ToTimeSpan(24, NizimaAutoOffUnits.Hours));
        Assert.True(NizimaAutoOffUnits.ToTimeSpan(1441, NizimaAutoOffUnits.Minutes) > NizimaAutoOffUnits.Max);
    }

    [Fact]
    public void ChannelPointTriggerShowsRewardTitle()
    {
        var rule = new NizimaTriggerRule
        {
            TriggerType = NizimaTriggerTypes.ChannelPoint,
            TriggerValue = "cp-1",
            CommandType = NizimaTriggerCommands.ChangeModel,
            CommandValue = @"C:\models\a.model3.json",
            ModelId = "model-1"
        };
        var rewards = new[] { new ChannelPointRewardInfo("cp-1", "にっこり", false) };
        var models = new[] { new NizimaNamedOption("MyModel", "model-1") };
        var text = NizimaTriggerRuleSummary.Format(
            rule,
            new NizimaRuleCatalogs { ModelsOnScreen = models },
            rewards);
        Assert.Contains("チャンネルポイント:にっこり", text);
        Assert.Contains("モデルを切り替え", text);
        Assert.DoesNotContain("cp-1", text);
    }

    [Fact]
    public void SummaryShowsFriendlyAdAndObsLabels()
    {
        var ad = new NizimaTriggerRule
        {
            TriggerType = NizimaTriggerTypes.AdUpcoming,
            TriggerValue = "3",
            CommandType = NizimaTriggerCommands.ExpressionOn
        };
        Assert.Contains("3 分前", NizimaTriggerRuleSummary.Format(ad));

        var obs = new NizimaTriggerRule
        {
            TriggerType = NizimaTriggerTypes.ObsStreamStart,
            TriggerValue = "main",
            CommandType = NizimaTriggerCommands.ExpressionOn
        };
        Assert.Contains("メイン OBS", NizimaTriggerRuleSummary.Format(obs));
    }
}

public class NizimaScheduledTimeFormatTests
{
    [Fact]
    public void FormatsHourAndMinuteWithZeroPad()
    {
        Assert.True(NizimaTriggerUi.TryFormatScheduledTime(9, 5, out var formatted));
        Assert.Equal("09:05", formatted);
    }

    [Fact]
    public void ParsesStoredValue()
    {
        Assert.True(NizimaTriggerUi.TryParseScheduledTime("21:05", out var hour, out var minute));
        Assert.Equal(21, hour);
        Assert.Equal(5, minute);
    }

    [Fact]
    public void PartsBuildCanonicalValue()
    {
        Assert.True(NizimaTriggerUi.TryParseScheduledParts("9", "5", out var value, out _));
        Assert.Equal("09:05", value);
    }

    [Theory]
    [InlineData("24", "0")]
    [InlineData("0", "60")]
    public void RejectsOutOfRange(string hour, string minute)
    {
        Assert.False(NizimaTriggerUi.TryParseScheduledParts(hour, minute, out _, out var error));
        Assert.False(string.IsNullOrWhiteSpace(error));
    }

    [Fact]
    public void FollowIgnoresTriggerValue()
    {
        var rule = new NizimaTriggerRule { TriggerType = NizimaTriggerTypes.Follow, TriggerValue = "" };
        Assert.True(NizimaTriggerMatcher.Matches(rule, NizimaTriggerTypes.Follow, "anything"));
    }
}

public class NizimaQtKeySequenceTests
{
    [Theory]
    [InlineData(Key.A, ModifierKeys.Control, "Ctrl+A")]
    [InlineData(Key.F1, ModifierKeys.None, "F1")]
    [InlineData(Key.F5, ModifierKeys.Shift, "Shift+F5")]
    [InlineData(Key.Return, ModifierKeys.Control | ModifierKeys.Shift, "Ctrl+Shift+Return")]
    public void FormatsPortableSequence(Key key, ModifierKeys modifiers, string expected)
    {
        Assert.True(NizimaQtKeySequence.TryFormat(key, modifiers, out var sequence));
        Assert.Equal(expected, sequence);
    }

    [Fact]
    public void RejectsWindowsModifier()
    {
        Assert.False(NizimaQtKeySequence.TryFormat(Key.A, ModifierKeys.Control | ModifierKeys.Windows, out _));
    }

    [Fact]
    public void RejectsModifierKeyAlone()
    {
        Assert.False(NizimaQtKeySequence.TryFormat(Key.LeftCtrl, ModifierKeys.Control, out _));
    }
}
