using JTSA.Utility;
using Xunit;

namespace JTSA.Tests;

public class StreamExpansionImageSettingsTests
{
    [Fact]
    public void EncodeAndDecodePreserveImageLayout()
    {
        var settings = new StreamExpansionImageSettings("C:\\image.png", 640, 360, 100, 200, true);

        var decoded = StreamExpansionImageSettings.Decode(settings.Encode());

        Assert.Equal(settings, decoded);
    }

    [Fact]
    public void DecodeLegacyPathUsesFullCanvasDefaults()
    {
        var decoded = StreamExpansionImageSettings.Decode("C:\\legacy.png");

        Assert.Equal("C:\\legacy.png", decoded.Path);
        Assert.Equal(1920, decoded.Width);
        Assert.Equal(1080, decoded.Height);
        Assert.Equal(0, decoded.X);
        Assert.Equal(0, decoded.Y);
        Assert.False(decoded.RandomPosition);
    }

    [Fact]
    public void NormalizeKeepsImageInsideCanvas()
    {
        var normalized = new StreamExpansionImageSettings("image.png", 500, 300, 1800, 1000).Normalize();

        Assert.Equal(1420, normalized.X);
        Assert.Equal(780, normalized.Y);
    }
}
