using LMCCore.Game.Download;
using LMCCore.Game.Download.Model;

namespace LMC.Tests;

public class DownloadSourceTests
{
    [Fact]
    public void TransformUrl_OfficialSourceKeepsRecognizedUrl()
    {
        var source = new OfficialDownloadSource();

        var result = source.TransformUrl("https://libraries.minecraft.net/com/example/lib.jar");

        Assert.Equal("https://libraries.minecraft.net/com/example/lib.jar", result);
    }

    [Fact]
    public void TransformUrl_BmclSourceTransformsKnownUrls()
    {
        var source = new BmclDownloadSource();

        var result = source.TransformUrl("https://resources.download.minecraft.net/ab/cdef");

        Assert.Equal("https://bmclapi2.bangbang93.com/assets/ab/cdef", result);
    }

    [Fact]
    public void TransformUrl_NormalizesHttpBeforeTransforming()
    {
        var manager = DownloadSourceManager.CreateDefault();

        var result = manager.TransformUrl("http://resources.download.minecraft.net/ab/cdef");

        Assert.Equal("https://bmclapi2.bangbang93.com/assets/ab/cdef", result);
    }

    [Fact]
    public void TransformUrlWithFallback_UsesFallbackWhenPrimaryDoesNotMatch()
    {
        var primary = new CustomDownloadSource("custom", new Dictionary<string, string>
        {
            ["https://example.com/"] = "https://mirror.example.com/"
        })
        {
            FallbackSource = new OfficialDownloadSource()
        };

        var result = primary.TransformUrlWithFallback("https://libraries.minecraft.net/com/example/lib.jar");

        Assert.Equal("https://libraries.minecraft.net/com/example/lib.jar", result);
    }

    [Fact]
    public void TransformUrl_CustomSourceLeavesUnknownUrlUntouched()
    {
        var source = new CustomDownloadSource("custom", new Dictionary<string, string>
        {
            ["https://a.example/"] = "https://b.example/"
        });

        var result = source.TransformUrl("https://unknown.example/file");

        Assert.Equal("https://unknown.example/file", result);
    }
}
