using JTSA.Utility;
using NAudio;
using NAudio.Wave;
using Xunit;

namespace JTSA.Tests;

public class NotificationAudioPlayerTests
{
    private static WaveStream Reader() => new RawSourceWaveStream(
        new MemoryStream(new byte[] { 0, 64, 0, 64 }), new WaveFormat(8000, 16, 1));

    [Theory]
    [InlineData("create")]
    [InlineData("init")]
    [InlineData("play")]
    [InlineData("stopped")]
    [InlineData("dispose")]
    public async Task DeviceFailureIsContainedAndNextNotificationRecovers(string failure)
    {
        var first = new FakeOutput { Failure = failure };
        var recovered = new FakeOutput();
        var calls = 0;
        var player = new NotificationAudioPlayer(() =>
        {
            if (++calls > 1) return recovered;
            if (failure == "create") throw new MmException(MmResult.NoDriver, "create");
            return first;
        });
        await player.TryPlayAsync(Reader, 0.5f);
        await player.TryPlayAsync(Reader, 0.5f);
        Assert.True(recovered.Played);
        Assert.True(recovered.Disposed);
        if (failure != "create") Assert.True(first.Disposed);
    }

    [Fact]
    public async Task VolumeIsAppliedToSamplesWithoutSettingDeviceVolume()
    {
        var output = new FakeOutput();
        await new NotificationAudioPlayer(() => output).TryPlayAsync(Reader, 0.5f);
        Assert.Equal(0.25f, output.FirstSample, 4);
    }

    [Fact]
    public async Task ConcurrentNotificationsDoNotReuseOrRewindPlayingReader()
    {
        var output = new FakeOutput { AutoComplete = false };
        var calls = 0;
        var player = new NotificationAudioPlayer(() => { calls++; return output; });
        var playing = player.TryPlayAsync(Reader, 1);
        await player.TryPlayAsync(Reader, 1);
        Assert.Equal(1, calls);
        Assert.False(playing.IsCompleted);
        output.Complete();
        await playing;
        Assert.True(output.Disposed);
    }

    [Fact]
    public async Task ReaderFailureAndMuteDoNotPreventLaterPlayback()
    {
        var output = new FakeOutput();
        var player = new NotificationAudioPlayer(() => output);
        await player.TryPlayAsync(() => throw new IOException("Missing audio file"), 1);
        await player.TryPlayAsync(() => throw new InvalidOperationException("Muted reader must not open"), 0);
        await player.TryPlayAsync(Reader, 1);
        Assert.True(output.Played);
    }

    private sealed class FakeOutput : IWavePlayer
    {
        public string? Failure { get; init; }
        public bool AutoComplete { get; init; } = true;
        public bool Played { get; private set; }
        public bool Disposed { get; private set; }
        public float FirstSample { get; private set; }
        public event EventHandler<StoppedEventArgs>? PlaybackStopped;
        public PlaybackState PlaybackState => PlaybackState.Stopped;
        public WaveFormat OutputWaveFormat { get; private set; } = WaveFormat.CreateIeeeFloatWaveFormat(8000, 1);
        public float Volume { get => 1; set => throw new InvalidOperationException("Device Volume must not be used"); }
        public void Init(IWaveProvider waveProvider)
        {
            if (Failure == "init") throw new MmException(MmResult.NoDriver, "Init");
            OutputWaveFormat = waveProvider.WaveFormat;
            var bytes = new byte[4];
            Assert.Equal(4, waveProvider.Read(bytes, 0, bytes.Length));
            FirstSample = BitConverter.ToSingle(bytes);
        }
        public void Play()
        {
            if (Failure == "play") throw new MmException(MmResult.NoDriver, "Play");
            Played = true;
            if (AutoComplete) Complete();
        }
        public void Complete() => PlaybackStopped?.Invoke(this,
            new StoppedEventArgs(Failure == "stopped" ? new MmException(MmResult.NoDriver, "Playback") : null));
        public void Stop() { }
        public void Pause() { }
        public void Dispose()
        {
            Disposed = true;
            if (Failure == "dispose") throw new MmException(MmResult.NoDriver, "Dispose");
        }
    }
}
