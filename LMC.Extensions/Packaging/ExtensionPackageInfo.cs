namespace LMC.Extensions.Packaging;

public class ExtensionPackageInfo
{
    public required string PackagePath { get; init; }

    public required string PackageHash { get; init; }

    public required ExtensionManifest Manifest { get; init; }
}
