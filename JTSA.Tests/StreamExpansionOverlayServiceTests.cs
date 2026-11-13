using System.IO;
using JTSA.Plugin.Abstractions;
using JTSA.Utility;
using Xunit;

namespace JTSA.Tests;

public class StreamExpansionOverlayServiceTests
{
    [Fact]
    public void UnchangedOverlayReusesJsonAndMutationsInvalidateIt()
    {
        var pluginId = Guid.NewGuid().ToString();
        var content = new ExpansionOverlayContent("test", "<b>hello</b>", 0, 0, 100, 100);
        try
        {
            StreamExpansionOverlayService.SetPluginOverlay(pluginId, content);
            var initial = StreamExpansionOverlayService.CreateJson();
            Assert.Same(initial, StreamExpansionOverlayService.CreateJson());
            StreamExpansionOverlayService.SetPluginOverlay(pluginId, content with { });
            Assert.Same(initial, StreamExpansionOverlayService.CreateJson());
            StreamExpansionOverlayService.SetPluginOverlay(pluginId, content with { Html = "updated" });
            var updated = StreamExpansionOverlayService.CreateJson();
            Assert.NotEqual(initial, updated);
            StreamExpansionOverlayService.RemovePluginOverlay(pluginId, content.Id);
            Assert.DoesNotContain(pluginId, StreamExpansionOverlayService.CreateJson());
        }
        finally
        {
            StreamExpansionOverlayService.RemovePluginOverlay(pluginId, content.Id);
        }
    }

    [Fact]
    public void OnlyRegisteredMediaFilesAreServed()
    {
        var path = Path.GetTempFileName();
        try
        {
            var url = StreamExpansionOverlayService.RegisterMediaFile(path);
            var id = url.Split("id=")[1].Split('&')[0];
            Assert.Equal(Path.GetFullPath(path), StreamExpansionOverlayService.GetMediaPath(id));
            Assert.Null(StreamExpansionOverlayService.GetMediaPath("unregistered"));
            File.Delete(path);
            Assert.Null(StreamExpansionOverlayService.GetMediaPath(id));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void DeletedImageInvalidatesCachedJson()
    {
        var path = Path.GetTempFileName();
        try
        {
            var initial = StreamExpansionOverlayService.CreateJson();
            StreamExpansionOverlayService.ShowImage(path);
            Assert.NotEqual(initial, StreamExpansionOverlayService.CreateJson());
            File.Delete(path);
            Assert.Equal(initial, StreamExpansionOverlayService.CreateJson());
        }
        finally
        {
            File.Delete(path);
        }
    }
}
