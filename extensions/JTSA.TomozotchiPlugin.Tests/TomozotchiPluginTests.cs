using System.IO;
using System.Text.Json;
using Xunit;

namespace JTSA.TomozotchiPlugin.Tests;

public class TomozotchiPluginTests
{
    // tamagotchi-twitch の game_config.json と同じ形（hearts などの旧項目も含む）
    private const string LegacyGameConfig = """
        {
          "stats": [
            { "name": "進捗", "maxHearts": 3, "initialHearts": 0, "decreaseInterval": 0, "markFull": "⭐️", "order": 2 },
            { "name": "おなか", "maxHearts": 4, "initialHearts": 4, "decreaseInterval": 60, "markFull": "🍚", "hearts": 4, "order": 0 },
            { "name": "おみず", "maxHearts": "3", "initialHearts": 3, "decreaseInterval": 30, "markFull": "💧", "order": 1 }
          ],
          "channelPoints": {
            "todoRewardIds": ["a48b82ae"],
            "rewardActions": [
              { "rewardId": "r-water", "action": "modify", "stat": "おみず", "amount": 1 },
              { "rewardId": "r-add", "action": "addStat", "statDef": { "name": "ねむけ", "maxHearts": 2, "initialHearts": 1, "decreaseInterval": 10, "markFull": "💤" } },
              { "rewardId": "r-remove", "action": "removeStat", "stat": "ねむけ" }
            ]
          }
        }
        """;

    private static string WriteTemp(string json)
    {
        var path = Path.Combine(Path.GetTempPath(), $"tomozotchi-{Guid.NewGuid():N}.json");
        File.WriteAllText(path, json);
        return path;
    }

    [Fact]
    public void ReadsLegacyGameConfigInOrderWithRewardActions()
    {
        var config = TomozotchiStore.ReadGameConfig(WriteTemp(LegacyGameConfig));

        Assert.Equal(["おなか", "おみず", "進捗"], config.Stats.Select(stat => stat.Name));
        Assert.Equal(3, config.Stats[1].MaxHearts);
        Assert.Equal(3, config.ChannelPoints!.RewardActions.Count);
        Assert.Equal("ねむけ", config.ChannelPoints.RewardActions[1].StatDef!.Name);
    }

    [Fact]
    public void SettingsRoundTripKeepsChannelPointStatusIds()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"tomozotchi-{Guid.NewGuid():N}");
        var settings = new TomozotchiSettings { Game = TomozotchiStore.ReadGameConfig(WriteTemp(LegacyGameConfig)) };

        TomozotchiStore.Save(directory, settings);
        var loaded = TomozotchiStore.Load(directory);

        Assert.Equal(["おなか", "おみず", "進捗"], loaded.Game.Stats.Select(stat => stat.Name));
        Assert.Equal(["a48b82ae"], loaded.Game.ChannelPoints!.TodoRewardIds);
        Assert.Contains("\"maxHearts\"", File.ReadAllText(Path.Combine(directory, "settings.json")));
    }

    [Fact]
    public void PresetHasNoChannelPointsSoLoadingKeepsRewardActions()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"tomozotchi-{Guid.NewGuid():N}");
        TomozotchiStore.SavePreset(directory, "テスト", [new StatDefinition { Name = "A", MaxHearts = 2 }]);

        var preset = TomozotchiStore.ReadGameConfig(TomozotchiStore.PresetPath(directory, "テスト"));

        Assert.Null(preset.ChannelPoints);
        Assert.Equal(["テスト"], TomozotchiStore.ListPresets(directory));
        Assert.Throws<ArgumentException>(() => TomozotchiStore.SavePreset(directory, "a/b", []));
    }

    [Fact]
    public void DecaysByIntervalAndCatchesUp()
    {
        var now = new DateTime(2026, 10, 4, 0, 0, 0, DateTimeKind.Utc);
        var game = new TomozotchiGame(TomozotchiStore.ReadGameConfig(WriteTemp(LegacyGameConfig)), () => now);
        var changes = 0;
        game.Changed += () => changes++;

        now = now.AddMinutes(29);
        game.Tick();
        Assert.Equal(3, game.ValueOf("おみず"));
        Assert.Equal(0, changes);

        now = now.AddMinutes(31); // 60 分経過
        game.Tick();
        Assert.Equal(1, game.ValueOf("おみず"));
        Assert.Equal(3, game.ValueOf("おなか"));
        Assert.Equal(0, game.ValueOf("進捗")); // 間隔 0 は減らない

        now = now.AddHours(5);
        game.Tick();
        Assert.Equal(0, game.ValueOf("おみず"));
        Assert.Equal(0, game.ValueOf("おなか"));
    }

    [Fact]
    public void RewardActionsModifyAddAndRemove()
    {
        var now = new DateTime(2026, 10, 4, 0, 0, 0, DateTimeKind.Utc);
        var game = new TomozotchiGame(TomozotchiStore.ReadGameConfig(WriteTemp(LegacyGameConfig)), () => now);

        Assert.True(game.FireReward("r-water")!.Success);
        Assert.Equal(3, game.ValueOf("おみず")); // 最大で頭打ち
        Assert.Null(game.Notice); // 実際に変わっていなければ通知しない

        game.SetValue("おみず", 1);
        game.FireReward("r-water");
        Assert.Equal(2, game.ValueOf("おみず"));
        Assert.Equal(("おみず +1 回復！", "recover"), (game.Notice!.Message, game.Notice.Kind));

        Assert.True(game.FireReward("r-add")!.Success);
        Assert.Equal(1, game.ValueOf("ねむけ"));
        Assert.False(game.FireReward("r-add")!.Success); // 重複

        Assert.True(game.FireReward("r-remove")!.Success);
        Assert.Null(game.Find("ねむけ"));
        Assert.Equal(3, game.RewardActions.Count); // 割り当ては残す
        Assert.Null(game.FireReward("unknown"));
    }

    [Fact]
    public void RenameKeepsValueAndUpdatesRewardActions()
    {
        var game = new TomozotchiGame(TomozotchiStore.ReadGameConfig(WriteTemp(LegacyGameConfig)));
        game.SetValue("おみず", 2);

        var result = game.UpdateStat("おみず", new StatDefinition { Name = "水", MaxHearts = 5, DecreaseInterval = 30, MarkFull = "💧" });

        Assert.True(result.Success);
        Assert.Equal(2, game.ValueOf("水"));
        Assert.Equal("水", game.RewardActions[0].Stat);
        Assert.False(game.UpdateStat("水", new StatDefinition { Name = "おなか", MaxHearts = 1 }).Success);
    }

    [Fact]
    public void ReplaceStatsKeepsValuesOfSameNames()
    {
        var game = new TomozotchiGame(TomozotchiStore.ReadGameConfig(WriteTemp(LegacyGameConfig)));
        game.SetValue("おなか", 1);
        var actions = game.RewardActions.ToList();

        game.ReplaceStats([new StatDefinition { Name = "おなか", MaxHearts = 9 }, new StatDefinition { Name = "新", MaxHearts = 2, InitialHearts = 1 }]);

        Assert.Equal(1, game.ValueOf("おなか"));
        Assert.Equal(1, game.ValueOf("新"));
        Assert.Equal(actions, game.RewardActions);
    }

    [Fact]
    public void OverlayRendersHeartsAndEscapesNames()
    {
        Assert.Equal(
            "<span class=\"tz-full\">♥</span><span class=\"tz-empty\">♡</span>",
            TomozotchiOverlay.Hearts(1, 2, "♥"));
        Assert.Contains("tz-empty-emoji\">🍚", System.Net.WebUtility.HtmlDecode(TomozotchiOverlay.Hearts(0, 1, "🍚")));
        Assert.Contains("<span class=\"tz-empty\">・</span>", TomozotchiOverlay.Hearts(0, 1, "★"));

        var game = new TomozotchiGame(new GameConfig { Stats = [new StatDefinition { Name = "<b>x</b>", MaxHearts = 1 }] });
        game.Change("<b>x</b>", -1);
        var html = TomozotchiOverlay.Render(game);
        Assert.Contains("&lt;b&gt;x&lt;/b&gt;", html);
        Assert.Contains("data-jtsa-animation-start=", html);
        Assert.DoesNotContain("<script", html);
    }
}
