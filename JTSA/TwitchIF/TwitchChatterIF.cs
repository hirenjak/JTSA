namespace JTSA.TwitchIF;

/// <summary>Twitchのチャットに接続しているユーザー。動画視聴の有無は表さない。</summary>
public sealed class TwitchChatterIF
{
    public string UserId { get; init; } = string.Empty;
    public string UserLogin { get; init; } = string.Empty;
    public string UserName { get; init; } = string.Empty;
}
