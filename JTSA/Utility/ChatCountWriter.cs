using System.Threading.Channels;
using JTSA.Dao;

namespace JTSA.Utility;

/// <summary>最大100件を100ms単位でまとめ、一つのトランザクションで保存する。</summary>
internal sealed class ChatCountWriter
{
    private sealed record Pending(DAO_StreamChatUserCount.ChatCountEntry Entry, TaskCompletionSource Completion);
    private readonly Channel<Pending> queue = Channel.CreateBounded<Pending>(new BoundedChannelOptions(2048)
    {
        SingleReader = true,
        FullMode = BoundedChannelFullMode.Wait
    });
    private readonly Task worker;

    public ChatCountWriter() => worker = Task.Run(RunAsync);

    public async Task RecordAsync(DateTime time, string userId, string loginId, string displayName, string streamId)
    {
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await queue.Writer.WriteAsync(new(new(time, userId, loginId, displayName, streamId), completion)).ConfigureAwait(false);
        await completion.Task.ConfigureAwait(false);
    }

    public async Task CompleteAsync()
    {
        queue.Writer.TryComplete();
        await worker.ConfigureAwait(false);
    }

    private async Task RunAsync()
    {
        while (await queue.Reader.WaitToReadAsync().ConfigureAwait(false))
        {
            await Task.Delay(100).ConfigureAwait(false);
            var batch = new List<Pending>(100);
            while (batch.Count < 100 && queue.Reader.TryRead(out var item)) batch.Add(item);
            try
            {
                DAO_StreamChatUserCount.IncrementBatch(batch.Select(x => x.Entry).ToList());
                foreach (var item in batch) item.Completion.SetResult();
            }
            catch (Exception ex)
            {
                foreach (var item in batch) item.Completion.SetException(ex);
            }
        }
    }
}
