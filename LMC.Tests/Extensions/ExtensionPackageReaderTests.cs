using System.IO.Compression;
using LMC.Extensions.Packaging;

namespace LMC.Tests.Extensions;

public class ExtensionPackageReaderTests
{
    private readonly ExtensionPackageReader _reader = new();

    [Fact]
    public void ScanPackages_ReadsValidManifest()
    {
        using var scope = new TestFileSystemScope();
        var extensionsDirectory = scope.CreateDirectory("extensions");
        var manifest = ExtensionPackageBuilder.CreateManifest(
            "sample.extension",
            "1.0.0",
            $"{nameof(LMC)}.dll",
            "Sample.Extension");
        ExtensionPackageBuilder.CreatePackage(
            Path.Combine(extensionsDirectory, "sample.lext"),
            manifest,
            includeEntryAssembly: false);

        var result = _reader.ScanPackages(extensionsDirectory);

        var package = Assert.Single(result.Packages);
        Assert.Equal("sample.extension", package.Manifest.Id);
        Assert.Empty(result.InvalidPackages);
    }

    [Fact]
    public void ScanPackages_ReportsMissingManifest()
    {
        using var scope = new TestFileSystemScope();
        var extensionsDirectory = scope.CreateDirectory("extensions");
        using (var archive = ZipFile.Open(Path.Combine(extensionsDirectory, "missing-manifest.lext"), ZipArchiveMode.Create))
        {
            archive.CreateEntry("content/readme.txt");
        }

        var result = _reader.ScanPackages(extensionsDirectory);

        Assert.Empty(result.Packages);
        Assert.Single(result.InvalidPackages);
    }

    [Fact]
    public void ScanPackages_ReportsMissingRequiredManifestField()
    {
        using var scope = new TestFileSystemScope();
        var extensionsDirectory = scope.CreateDirectory("extensions");
        var manifest = new ExtensionManifest
        {
            Id = "sample.extension",
            Name = string.Empty,
            Version = "1.0.0",
            EntryAssembly = "sample.dll",
            EntryType = "Sample.Extension"
        };

        ExtensionPackageBuilder.CreatePackage(
            Path.Combine(extensionsDirectory, "missing-name.lext"),
            manifest,
            includeEntryAssembly: false);

        var result = _reader.ScanPackages(extensionsDirectory);

        Assert.Empty(result.Packages);
        var invalid = Assert.Single(result.InvalidPackages);
        Assert.Contains("name is required", invalid.Message);
    }

    [Fact]
    public void ScanPackages_ReportsInvalidDependencyKind()
    {
        using var scope = new TestFileSystemScope();
        var extensionsDirectory = scope.CreateDirectory("extensions");
        var manifest = ExtensionPackageBuilder.CreateManifest(
            "sample.extension",
            "1.0.0",
            "sample.dll",
            "Sample.Extension",
            dependencies: [("dependency", "[1.0.0,2.0.0)", "optional")]);

        ExtensionPackageBuilder.CreatePackage(
            Path.Combine(extensionsDirectory, "invalid-kind.lext"),
            manifest,
            includeEntryAssembly: false);

        var result = _reader.ScanPackages(extensionsDirectory);

        Assert.Empty(result.Packages);
        var invalid = Assert.Single(result.InvalidPackages);
        Assert.Contains("hard or soft", invalid.Message);
    }

    [Fact]
    public void ScanPackages_ReportsCorruptedZip()
    {
        using var scope = new TestFileSystemScope();
        var extensionsDirectory = scope.CreateDirectory("extensions");
        File.WriteAllText(Path.Combine(extensionsDirectory, "broken.lext"), "not a zip");

        var result = _reader.ScanPackages(extensionsDirectory);

        Assert.Empty(result.Packages);
        Assert.Single(result.InvalidPackages);
    }
}
