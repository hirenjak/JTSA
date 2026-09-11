using System.Collections.Specialized;
using JTSA.Utility;
using Xunit;

namespace JTSA.Tests;

public class PerformanceUtilityTests
{
    [Fact]
    public async Task CacheSharesConcurrentRequestsAndRetriesFailures()
    {
        var cache = new AsyncCache<string, int>(2, TimeSpan.FromMinutes(1));
        var release = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        var calls = 0;
        Task<int> Load() { Interlocked.Increment(ref calls); return release.Task; }
        var requests = Enumerable.Range(0, 50).Select(_ => cache.GetAsync("same", Load)).ToArray();
        release.SetResult(42);
        Assert.All(await Task.WhenAll(requests), value => Assert.Equal(42, value));
        Assert.Equal(1, calls);
        await Assert.ThrowsAsync<InvalidOperationException>(() => cache.GetAsync("failed", () => Task.FromException<int>(new InvalidOperationException())));
        Assert.Equal(7, await cache.GetAsync("failed", () => Task.FromResult(7)));
        cache.Remove("same");
        Assert.Equal(9, await cache.GetAsync("same", () => Task.FromResult(9)));
    }

    [Fact]
    public async Task CacheHonorsCapacityAndExpiry()
    {
        var cache = new AsyncCache<string, int>(1, TimeSpan.FromMinutes(1));
        await cache.GetAsync("a", () => Task.FromResult(1));
        await cache.GetAsync("b", () => Task.FromResult(2));
        Assert.Equal(3, await cache.GetAsync("a", () => Task.FromResult(3)));
        var expired = new AsyncCache<string, int>(1, TimeSpan.Zero);
        await expired.GetAsync("a", () => Task.FromResult(1));
        Assert.Equal(2, await expired.GetAsync("a", () => Task.FromResult(2)));
    }

    [Fact]
    public void BulkReplacementNotifiesOnceAndSkipsUnchangedValues()
    {
        var collection = new BatchObservableCollection<int>();
        var notifications = new List<NotifyCollectionChangedAction>();
        collection.CollectionChanged += (_, args) => notifications.Add(args.Action);
        collection.ReplaceAll(Enumerable.Range(0, 1000));
        Assert.Equal(new[] { NotifyCollectionChangedAction.Reset }, notifications);
        collection.ReplaceAll(collection);
        Assert.Single(notifications);
        collection.ReplaceAll([]);
        Assert.Empty(collection);
        Assert.Equal(2, notifications.Count);
    }

    [Fact]
    public void ImageCacheReusesDecodedImageAtRequestedSize()
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            var path = Path.Combine(Path.GetTempPath(), $"jtsa-image-{Guid.NewGuid():N}.png");
            try
            {
                var source = System.Windows.Media.Imaging.BitmapSource.Create(256, 256, 96, 96,
                    System.Windows.Media.PixelFormats.Bgra32, null, new byte[256 * 256 * 4], 256 * 4);
                var encoder = new System.Windows.Media.Imaging.PngBitmapEncoder();
                encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(source));
                using (var file = File.Create(path)) encoder.Save(file);
                var url = new Uri(path).AbsoluteUri;
                var image = CachedImageConverter.GetImage(url, 64);
                Assert.Equal(64, image.PixelWidth);
                Assert.Same(image, CachedImageConverter.GetImage(url, 64));
                Assert.Equal(128, CachedImageConverter.GetImage(url, 128).PixelWidth);
            }
            catch (Exception ex) { failure = ex; }
            finally { try { File.Delete(path); } catch (IOException) { } }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (failure != null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw();
    }
}
