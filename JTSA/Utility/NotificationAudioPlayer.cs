using System.Diagnostics;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace JTSA.Utility;

/// <summary>通知音の失敗をチャット処理へ伝播させず、次回は新しい出力デバイスで再生する。</summary>
internal sealed class NotificationAudioPlayer
{
    private readonly Func<IWavePlayer> createOutput;
    private readonly SemaphoreSlim playbackGate = new(1, 1);

    internal NotificationAudioPlayer(Func<IWavePlayer>? createOutput = null)
    {
        this.createOutput = createOutput ?? (() => new WaveOutEvent());
    }

    public async Task TryPlayAsync(Func<WaveStream> createReader, float volume)
    {
        // 同じ通知音の再生中はスキップ。再生中のReaderを巻き戻さない。
        if (!await playbackGate.WaitAsync(0)) return;
        IWavePlayer? output = null;
        WaveStream? reader = null;
        EventHandler<StoppedEventArgs>? stopped = null;
        try
        {
            if (volume <= 0) return;
            reader = createReader();
            var samples = new VolumeSampleProvider(reader.ToSampleProvider())
            {
                Volume = Math.Clamp(volume, 0f, 1f)
            };
            output = createOutput();
            var completion = new TaskCompletionSource<Exception?>(TaskCreationOptions.RunContinuationsAsynchronously);
            stopped = (_, args) => completion.TrySetResult(args.Exception);
            output.PlaybackStopped += stopped;
            // IWavePlayer.VolumeはwaveOutSetVolumeを呼ぶため使用しない。
            output.Init(samples.ToWaveProvider());
            output.Play();
            var error = await completion.Task.ConfigureAwait(false);
            if (error is not null) Log(error);
        }
        catch (Exception ex)
        {
            Log(ex);
        }
        finally
        {
            // 初期化失敗・デバイス消失・Dispose失敗も通知音の中で完結させる。
            try
            {
                if (output is not null)
                {
                    if (stopped is not null) output.PlaybackStopped -= stopped;
                    output.Dispose();
                }
            }
            catch (Exception ex) { Log(ex); }
            try { reader?.Dispose(); }
            catch (Exception ex) { Log(ex); }
            playbackGate.Release();
        }
    }

    private static void Log(Exception ex) => Debug.WriteLine($"通知音再生をスキップ: {ex}");
}
