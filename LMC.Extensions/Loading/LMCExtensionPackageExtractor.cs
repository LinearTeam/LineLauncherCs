using System.IO.Compression;
using LMC.Extensions.Abstractions;
using LMC.Extensions.Packaging;

namespace LMC.Extensions.Loading;

public sealed class LMCExtensionPackageExtractor : ILMCExtensionPackageExtractor
{
    private readonly ILMCExtensionLogger _logger;

    public LMCExtensionPackageExtractor(ILMCExtensionLogger? logger = null)
    {
        _logger = logger ?? NullLMCExtensionLoggerFactory.Instance.CreateLogger(nameof(LMCExtensionPackageExtractor));
    }

    public string Extract(ExtensionPackageInfo package, string extensionsDirectory)
    {
        var cacheDirectory = Path.Combine(
            extensionsDirectory,
            ".cache",
            package.Manifest.Id,
            package.Manifest.Version,
            package.PackageHash);

        if (Directory.Exists(cacheDirectory))
        {
            _logger.Debug($"复用扩展缓存目录：{package.Manifest.Id} {package.Manifest.Version} -> {cacheDirectory}");
            return cacheDirectory;
        }

        _logger.Info($"开始解包扩展：{package.Manifest.Id} {package.Manifest.Version} -> {cacheDirectory}");
        Directory.CreateDirectory(Path.GetDirectoryName(cacheDirectory)!);
        ZipFile.ExtractToDirectory(package.PackagePath, cacheDirectory);
        _logger.Info($"扩展解包完成：{package.Manifest.Id} {package.Manifest.Version}");
        return cacheDirectory;
    }
}
