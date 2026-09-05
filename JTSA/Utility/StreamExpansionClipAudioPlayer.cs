using System.Diagnostics;
using NAudio.Wave;

namespace JTSA.Utility;

/// <summary>クリップの音声をJTSAの既定の再生デバイスへ出力する。</summary>
internal static class StreamExpansionClipAudioPlayer
{
    private static readonly SemaphoreSlim PlaybackLock = new(1, 1);
    private static IWavePlayer? currentOutput;
    private static WaveStream? currentReader;
    private static bool isStarted;

    public static async Task PrepareAsync(string videoUrl)
    {
        await PlaybackLock.WaitAsync();
        try
        {
            StopCurrent();

            var prepared = await Task.Run(() =>
            {
                var reader = new MediaFoundationReader(videoUrl);
                try
                {
                    var output = new WaveOutEvent();
                    output.Init(reader);
                    return (Reader: (WaveStream)reader, Output: (IWavePlayer)output);
                }
                catch
                {
                    reader.Dispose();
                    throw;
                }
            });

            currentReader = prepared.Reader;
            currentOutput = prepared.Output;
            isStarted = false;
            var ownedOutput = prepared.Output;
            ownedOutput.PlaybackStopped += (_, args) =>
            {
                if (args.Exception is not null)
                    Debug.WriteLine($"クリップ音声再生失敗: {args.Exception}");
                _ = StopIfCurrentAsync(ownedOutput);
            };
        }
        catch (Exception ex)
        {
            StopCurrent();
            throw new InvalidOperationException(
                "クリップ音声をアプリから再生できませんでした。既定の音声出力を確認してください。", ex);
        }
        finally
        {
            PlaybackLock.Release();
        }
    }

    public static void StartPrepared()
    {
        PlaybackLock.Wait();
        try
        {
            if (currentOutput is null)
                throw new InvalidOperationException("クリップ音声が準備されていません。");
            if (isStarted) return;
            currentOutput.Play();
            isStarted = true;
        }
        finally
        {
            PlaybackLock.Release();
        }
    }

    private static async Task StopIfCurrentAsync(IWavePlayer output)
    {
        await PlaybackLock.WaitAsync();
        try
        {
            if (ReferenceEquals(currentOutput, output)) StopCurrent();
        }
        finally
        {
            PlaybackLock.Release();
        }
    }

    private static void StopCurrent()
    {
        var output = currentOutput;
        var reader = currentReader;
        currentOutput = null;
        currentReader = null;
        isStarted = false;
        try { output?.Stop(); }
        catch (Exception ex) { Debug.WriteLine($"クリップ音声停止失敗: {ex}"); }
        try { output?.Dispose(); }
        catch (Exception ex) { Debug.WriteLine($"クリップ音声出力破棄失敗: {ex}"); }
        try { reader?.Dispose(); }
        catch (Exception ex) { Debug.WriteLine($"クリップ音声読込破棄失敗: {ex}"); }
    }
}
