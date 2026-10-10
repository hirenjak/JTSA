using Xunit;

namespace JTSA.TomozotchiPlugin.Tests;

public class ChannelPointStatusTests
{
    private static readonly DateTimeOffset Now = new(2026, 2, 26, 18, 15, 30, TimeSpan.Zero);

    // 新しい本体の IJtsaChannelPointStatusPluginContext と同じ名前・形のメソッドとレコード
    public sealed record FakeStatus(string Id, string Title, int Cost, string ImageUrl, bool IsEnabled, bool IsPaused,
        bool IsInStock, int GlobalCooldownSeconds, DateTimeOffset? CooldownExpiresAt, int? RedemptionsRedeemedCurrentStream, bool IsManageable);
    public sealed record FakePending(string Id, string RewardId, string RewardTitle, string UserName, string UserInput, DateTimeOffset RedeemedAt);
    public sealed record FakeRedemption(string RewardId, string UserName, string UserInput) { public string RedemptionId { get; init; } = ""; }

    public sealed class NewHost
    {
        public List<(string, string, bool)> Completed { get; } = [];

        public Task<IReadOnlyList<FakeStatus>> GetChannelPointRewardStatusesAsync() =>
            Task.FromResult<IReadOnlyList<FakeStatus>>(
            [
                new("cd", "💪筋トレ", 300, "", true, false, true, 270, Now.AddSeconds(245), 5, true),
                new("ok", "🍚ごはん", 100, "https://x/2.png", true, false, true, 0, null, null, false),
                new("paused", "おやすみ", 100, "", true, true, true, 0, null, null, false)
            ]);

        public Task<IReadOnlyList<FakePending>> GetUnfulfilledRedemptionsAsync(string rewardId) =>
            Task.FromResult<IReadOnlyList<FakePending>>([new("x1", rewardId, "💪筋トレ", "viewer", "スクワット", Now)]);

        public Task<bool> CompleteRedemptionAsync(string rewardId, string redemptionId, bool fulfilled)
        {
            Completed.Add((rewardId, redemptionId, fulfilled));
            return Task.FromResult(true);
        }
    }

    [Fact]
    public async Task ReadsNewHostThroughReflection()
    {
        var host = new NewHost();
        var source = ChannelPointStatusSource.TryCreate(host)!;

        var statuses = await source.GetRewardStatusesAsync();
        Assert.Equal(["cd", "ok", "paused"], statuses.Select(status => status.Id));
        Assert.Equal(Now.AddSeconds(245), statuses[0].CooldownExpiresAt);
        Assert.True(statuses[0].IsManageable);
        Assert.Equal("https://x/2.png", statuses[1].ImageUrl);

        var todos = await source.GetUnfulfilledAsync("cd");
        Assert.Equal(("x1", "cd", "viewer", "スクワット"), (todos[0].Id, todos[0].RewardId, todos[0].UserName, todos[0].UserInput));

        Assert.True(await source.CompleteAsync("cd", "x1", true));
        Assert.Equal([("cd", "x1", true)], host.Completed);
    }

    [Fact]
    public void OldHostIsNotSupported()
    {
        Assert.Null(ChannelPointStatusSource.TryCreate(new object()));
        Assert.Null(ChannelPointStatusSource.RedemptionIdOf(new { RewardId = "r" }));
        Assert.Equal("x9", ChannelPointStatusSource.RedemptionIdOf(new FakeRedemption("r", "u", "") { RedemptionId = "x9" }));
    }

    [Fact]
    public async Task BoardListsCheckedCooldownsRedeemablesAndTodos()
    {
        var board = new ChannelPointBoard();
        board.SetStatuses(await ChannelPointStatusSource.TryCreate(new NewHost())!.GetRewardStatusesAsync());
        var hooks = new ChannelPointHooks
        {
            CooldownRewardIds = ["cd", "ok"],
            RedeemableRewardIds = ["cd", "ok", "paused"],
            TodoRewardIds = ["cd"]
        };

        Assert.Equal(["cd"], board.Cooldowns(hooks, Now).Select(status => status.Id));
        Assert.Equal(["ok"], board.Redeemables(hooks, Now).Select(status => status.Id));
        // クールダウンが明けたら交換可能に移る
        Assert.Empty(board.Cooldowns(hooks, Now.AddMinutes(5)));
        Assert.Equal(["cd", "ok"], board.Redeemables(hooks, Now.AddMinutes(5)).Select(status => status.Id));

        board.AddTodo(new TodoItem("local", "cd", "💪筋トレ", "a", "", Now.AddSeconds(-10)));
        board.ReplaceTodos("cd", [new TodoItem("x1", "cd", "💪筋トレ", "b", "", Now)]);
        Assert.Equal(["x1"], board.Todos.Select(todo => todo.Id));
        Assert.True(board.RemoveTodo("x1"));
        Assert.Empty(board.Todos);
    }

    [Fact]
    public async Task OverlayShowsStatusLikeOriginalApp()
    {
        var board = new ChannelPointBoard();
        board.SetStatuses(await ChannelPointStatusSource.TryCreate(new NewHost())!.GetRewardStatusesAsync());
        var config = GameConfig.CreateDefault();
        config.ChannelPoints = new() { CooldownRewardIds = ["cd"], RedeemableRewardIds = ["ok", "noimage"], TodoRewardIds = ["cd"] };
        board.AddTodo(new TodoItem("x1", "cd", "<筋トレ>", "b", "", Now));

        var html = TomozotchiOverlay.Render(new TomozotchiGame(config), board, Now);

        Assert.Contains("⌛️クールダウン中 &#128170;筋トレ 4m05s", html);
        Assert.Contains("交換可能：", html);
        Assert.Contains("<img src=\"https://x/2.png\"", html);
        Assert.Contains("&lt;筋トレ&gt;</div>", html);
        // 再描画しても登場時刻は変わらない（アニメーションをやり直さない）
        var start = Now.ToUnixTimeMilliseconds().ToString();
        Assert.Contains($"data-jtsa-animation-start=\"{start}\"", TomozotchiOverlay.Render(new TomozotchiGame(config), board, Now.AddSeconds(1)));
    }

    [Theory]
    [InlineData(245, "4m05s")]
    [InlineData(12.2, "13s")]
    [InlineData(-1, "0s")]
    public void FormatsRemainingTime(double seconds, string expected) =>
        Assert.Equal(expected, ChannelPointBoard.FormatRemaining(TimeSpan.FromSeconds(seconds)));

    [Theory]
    [InlineData("💪筋トレ", "💪")]
    [InlineData("筋トレ", "🎁")]
    [InlineData("", "🎁")]
    public void UsesLeadingEmojiWhenNoImage(string title, string expected) =>
        Assert.Equal(expected, TomozotchiOverlay.LeadingEmoji(title));
}
