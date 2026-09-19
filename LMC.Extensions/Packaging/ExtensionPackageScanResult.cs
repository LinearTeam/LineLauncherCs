namespace LMC.Extensions.Packaging;

public class ExtensionPackageScanResult
{
    public IReadOnlyList<ExtensionPackageInfo> Packages { get; init; } = [];

    public IReadOnlyList<InvalidExtensionPackage> InvalidPackages { get; init; } = [];
}
