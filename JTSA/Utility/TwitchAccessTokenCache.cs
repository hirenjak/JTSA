namespace JTSA.Utility;

// 呼び出し側のトークン更新ロック内で使用する。再認証された場合は
// DBのRefresh Tokenとの不一致で古いキャッシュを使わない。
internal sealed class TwitchAccessTokenCache
{
    private readonly Dictionary<long, Entry> entries = new();

    internal void Store(long accountId, string refreshToken, string accessToken,
        int expiresIn, DateTimeOffset issuedAt)
        => entries[accountId] = new(refreshToken, accessToken,
            issuedAt.AddSeconds(Math.Max(0, expiresIn)),
            TimeSpan.FromSeconds(Math.Min(300, Math.Max(0, expiresIn) / 2.0)));

    internal bool TryGet(long accountId, string refreshToken, DateTimeOffset now, out string accessToken)
    {
        accessToken = string.Empty;
        if (!entries.TryGetValue(accountId, out var entry) ||
            entry.RefreshToken != refreshToken || now >= entry.ExpiresAt - entry.RefreshMargin)
            return false;

        accessToken = entry.AccessToken;
        return true;
    }

    private sealed record Entry(string RefreshToken, string AccessToken,
        DateTimeOffset ExpiresAt, TimeSpan RefreshMargin);
}
