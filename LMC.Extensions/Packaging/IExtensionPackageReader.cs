namespace LMC.Extensions.Packaging;

public interface IExtensionPackageReader
{
    ExtensionPackageScanResult ScanPackages(string extensionDirectory);
}
