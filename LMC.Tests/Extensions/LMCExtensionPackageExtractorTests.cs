using LMC.Extensions.Loading;

namespace LMC.Tests.Extensions;

public class LMCExtensionPackageExtractorTests
{
    private readonly LMCExtensionPackageExtractor _extractor = new();

    [Fact]
    public void Extract_CreatesCacheDirectoryOnFirstUse()
    {
        using var scope = new TestFileSystemScope();
        var extensionsDirectory = scope.CreateDirectory("extensions");
        var manifest = ExtensionPackageBuilder.CreateManifest(
            "sample.extension",
            "1.0.0",
            "sample.dll",
            "Sample.Extension");
        var packagePath = ExtensionPackageBuilder.CreatePackage(
            Path.Combine(extensionsDirectory, "sample.lext"),
            manifest,
            entryType: null,
            includeManifest: true,
            includeEntryAssembly: false,
            ("content/readme.txt", "hello"));
        var package = ExtensionPackageBuilder.CreatePackageInfo(packagePath, manifest);

        var cacheDirectory = _extractor.Extract(package, extensionsDirectory);

        Assert.True(Directory.Exists(cacheDirectory));
        Assert.True(File.Exists(Path.Combine(cacheDirectory, "manifest.json")));
        Assert.True(File.Exists(Path.Combine(cacheDirectory, "content", "readme.txt")));
    }

    [Fact]
    public void Extract_ReusesExistingCacheDirectory()
    {
        using var scope = new TestFileSystemScope();
        var extensionsDirectory = scope.CreateDirectory("extensions");
        var manifest = ExtensionPackageBuilder.CreateManifest(
            "sample.extension",
            "1.0.0",
            "sample.dll",
            "Sample.Extension");
        var packagePath = ExtensionPackageBuilder.CreatePackage(
            Path.Combine(extensionsDirectory, "sample.lext"),
            manifest,
            includeEntryAssembly: false);
        var package = ExtensionPackageBuilder.CreatePackageInfo(packagePath, manifest);

        var firstCacheDirectory = _extractor.Extract(package, extensionsDirectory);
        File.WriteAllText(Path.Combine(firstCacheDirectory, "marker.txt"), "cached");

        var secondCacheDirectory = _extractor.Extract(package, extensionsDirectory);

        Assert.Equal(firstCacheDirectory, secondCacheDirectory);
        Assert.True(File.Exists(Path.Combine(secondCacheDirectory, "marker.txt")));
    }

    [Fact]
    public void Extract_UsesExpectedCacheHierarchy()
    {
        using var scope = new TestFileSystemScope();
        var extensionsDirectory = scope.CreateDirectory("extensions");
        var manifest = ExtensionPackageBuilder.CreateManifest(
            "sample.extension",
            "1.0.0",
            "sample.dll",
            "Sample.Extension");
        var packagePath = ExtensionPackageBuilder.CreatePackage(
            Path.Combine(extensionsDirectory, "sample.lext"),
            manifest,
            includeEntryAssembly: false);
        var package = ExtensionPackageBuilder.CreatePackageInfo(packagePath, manifest);

        var cacheDirectory = _extractor.Extract(package, extensionsDirectory);

        var expectedPath = Path.Combine(
            extensionsDirectory,
            ".cache",
            manifest.Id,
            manifest.Version,
            package.PackageHash);
        Assert.Equal(expectedPath, cacheDirectory);
    }
}
