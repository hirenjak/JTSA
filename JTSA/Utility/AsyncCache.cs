namespace JTSA.Utility;

/// <summary>進行中の取得を共有し、件数と有効期限を制限するキャッシュ。</summary>
internal sealed class AsyncCache<TKey, TValue>(int capacity, TimeSpan lifetime) where TKey : notnull
{
    private readonly object sync = new();
    private readonly Dictionary<TKey, (Task<TValue> Task, DateTime Expires)> entries = [];

    public Task<TValue> GetAsync(TKey key, Func<Task<TValue>> factory)
    {
        lock (sync)
        {
            if (entries.TryGetValue(key, out var entry) &&
                (!entry.Task.IsCompleted || entry.Expires > DateTime.UtcNow) &&
                !entry.Task.IsFaulted && !entry.Task.IsCanceled)
                return entry.Task;
            entries.Remove(key);
            if (entries.Count >= capacity) entries.Remove(entries.Keys.First());
            // DB検索などfactoryの同期部分も呼び出し元のUIスレッドでは実行しない。
            var task = Task.Run(factory);
            entries[key] = (task, DateTime.UtcNow.Add(lifetime));
            return task;
        }
    }

    public void Remove(TKey key) { lock (sync) entries.Remove(key); }
}
