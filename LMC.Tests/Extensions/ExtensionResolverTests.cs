using LMC.Extensions.Resolution;
using LMC.Extensions.Runtime;

namespace LMC.Tests.Extensions;

public class ExtensionResolverTests
{
    private readonly ExtensionResolver _resolver = new();

    [Fact]
    public void Resolve_SelectsHighestCompatibleVersion()
    {
        var packages = new[]
        {
            ExtensionPackageBuilder.CreatePackageInfo("Sample", "1.0.0", "[3.0.0,4.0.0)"),
            ExtensionPackageBuilder.CreatePackageInfo("Sample", "2.0.0", "[2.0.0,4.0.0)"),
            ExtensionPackageBuilder.CreatePackageInfo("Sample", "3.0.0", "[4.0.0,5.0.0)")
        };

        var result = _resolver.Resolve(packages, "3.0.0");

        var resolved = Assert.Single(result.ResolvedExtensions);
        Assert.Equal("2.0.0", resolved.Package.Manifest.Version);
    }

    [Fact]
    public void Resolve_SkipsIncompatibleLauncherWhenNoCompatibleVersionExists()
    {
        var packages = new[]
        {
            ExtensionPackageBuilder.CreatePackageInfo("Sample", "1.0.0", "[4.0.0,5.0.0)")
        };

        var result = _resolver.Resolve(packages, "3.0.0");

        Assert.Empty(result.ResolvedExtensions);
        Assert.Contains(result.UnresolvedExtensions, item =>
            item.ExtensionId == "Sample" &&
            item.Status == ExtensionLoadStatus.SkippedIncompatibleLauncher);
    }

    [Fact]
    public void Resolve_SkipsMissingHardDependency()
    {
        var packages = new[]
        {
            ExtensionPackageBuilder.CreatePackageInfo("Blocked", "1.0.0", "[3.0.0,4.0.0)", ("Missing", "[1.0.0,2.0.0)", "hard"))
        };

        var result = _resolver.Resolve(packages, "3.0.0");

        Assert.Empty(result.ResolvedExtensions);
        Assert.Contains(result.UnresolvedExtensions, item =>
            item.ExtensionId == "Blocked" &&
            item.Status == ExtensionLoadStatus.SkippedMissingHardDependency);
    }

    [Fact]
    public void Resolve_SkipsUnsatisfiedHardDependencyVersion()
    {
        var packages = new[]
        {
            ExtensionPackageBuilder.CreatePackageInfo("Dependency", "2.0.0", "[3.0.0,4.0.0)"),
            ExtensionPackageBuilder.CreatePackageInfo("Blocked", "1.0.0", "[3.0.0,4.0.0)", ("Dependency", "[1.0.0,2.0.0)", "hard"))
        };

        var result = _resolver.Resolve(packages, "3.0.0");

        Assert.Single(result.ResolvedExtensions);
        Assert.Contains(result.UnresolvedExtensions, item =>
            item.ExtensionId == "Blocked" &&
            item.Status == ExtensionLoadStatus.SkippedMissingHardDependency);
    }

    [Fact]
    public void Resolve_InvalidatesHardDependencyCycle()
    {
        var packages = new[]
        {
            ExtensionPackageBuilder.CreatePackageInfo("A", "1.0.0", "[3.0.0,4.0.0)", ("B", "[1.0.0,2.0.0)", "hard")),
            ExtensionPackageBuilder.CreatePackageInfo("B", "1.0.0", "[3.0.0,4.0.0)", ("A", "[1.0.0,2.0.0)", "hard"))
        };

        var result = _resolver.Resolve(packages, "3.0.0");

        Assert.Empty(result.ResolvedExtensions);
        Assert.Equal(2, result.UnresolvedExtensions.Count);
        Assert.All(result.UnresolvedExtensions, item =>
            Assert.Equal(ExtensionLoadStatus.SkippedMissingHardDependency, item.Status));
    }

    [Fact]
    public void Resolve_AllowsMissingSoftDependency()
    {
        var packages = new[]
        {
            ExtensionPackageBuilder.CreatePackageInfo("SoftOnly", "1.0.0", "[3.0.0,4.0.0)", ("Missing", "[1.0.0,2.0.0)", "soft"))
        };

        var result = _resolver.Resolve(packages, "3.0.0");

        var resolved = Assert.Single(result.ResolvedExtensions);
        Assert.Equal("SoftOnly", resolved.Package.Manifest.Id);
        Assert.Empty(resolved.SoftDependencies);
        Assert.Empty(result.UnresolvedExtensions);
    }

    [Fact]
    public void Resolve_ProducesStableOrderWithMixedHardAndSoftDependencies()
    {
        var packages = new[]
        {
            ExtensionPackageBuilder.CreatePackageInfo("Core", "1.0.0", "[3.0.0,4.0.0)"),
            ExtensionPackageBuilder.CreatePackageInfo("Optional", "1.0.0", "[3.0.0,4.0.0)", ("Core", "[1.0.0,2.0.0)", "soft")),
            ExtensionPackageBuilder.CreatePackageInfo("Feature", "1.0.0", "[3.0.0,4.0.0)", ("Optional", "[1.0.0,2.0.0)", "hard"))
        };

        var result = _resolver.Resolve(packages, "3.0.0");

        Assert.Equal(["Core", "Optional", "Feature"], result.ResolvedExtensions.Select(item => item.Package.Manifest.Id));
    }
}
