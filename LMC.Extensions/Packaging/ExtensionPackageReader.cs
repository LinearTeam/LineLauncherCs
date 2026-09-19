using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using LMC.Extensions.Abstractions;

namespace LMC.Extensions.Packaging;

public sealed class ExtensionPackageReader : IExtensionPackageReader
{
    private static readonly JsonSerializerOptions s_serializerOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };
    private readonly ILMCExtensionLogger _logger;

    public ExtensionPackageReader(ILMCExtensionLogger? logger = null)
    {
        _logger = logger ?? NullLMCExtensionLoggerFactory.Instance.CreateLogger(nameof(ExtensionPackageReader));
    }

    public ExtensionPackageScanResult ScanPackages(string extensionDirectory)
    {
        if (!Directory.Exists(extensionDirectory))
        {
            _logger.Debug($"扩展目录不存在，跳过扫描：{extensionDirectory}");
            return new ExtensionPackageScanResult();
        }

        _logger.Info($"开始扫描扩展目录：{extensionDirectory}");
        var packages = new List<ExtensionPackageInfo>();
        var invalidPackages = new List<InvalidExtensionPackage>();

        foreach (var packagePath in Directory.EnumerateFiles(extensionDirectory, "*.lext", SearchOption.TopDirectoryOnly))
        {
            try
            {
                var package = ReadPackage(packagePath);
                packages.Add(package);
                _logger.Debug($"扩展包读取成功：{package.Manifest.Id} {package.Manifest.Version}，文件：{packagePath}");
            }
            catch (Exception ex)
            {
                _logger.Warn($"扩展包读取失败，已跳过：{packagePath}，原因：{ex.Message}");
                invalidPackages.Add(new InvalidExtensionPackage
                {
                    PackagePath = packagePath,
                    Message = ex.Message
                });
            }
        }

        _logger.Info($"扩展目录扫描完成：有效 {packages.Count} 个，无效 {invalidPackages.Count} 个。");
        return new ExtensionPackageScanResult
        {
            Packages = packages.AsReadOnly(),
            InvalidPackages = invalidPackages.AsReadOnly()
        };
    }

    private static ExtensionPackageInfo ReadPackage(string packagePath)
    {
        using var archive = ZipFile.OpenRead(packagePath);
        var manifestEntry = archive.GetEntry("manifest.json");
        if (manifestEntry == null)
        {
            throw new InvalidDataException("Extension package does not contain manifest.json.");
        }

        using var stream = manifestEntry.Open();
        var manifest = JsonSerializer.Deserialize<ExtensionManifest>(stream, s_serializerOptions);
        if (manifest == null)
        {
            throw new InvalidDataException("Extension manifest could not be deserialized.");
        }

        ValidateManifest(manifest);

        return new ExtensionPackageInfo
        {
            PackagePath = packagePath,
            PackageHash = ComputeSha256(packagePath),
            Manifest = manifest
        };
    }

    private static void ValidateManifest(ExtensionManifest manifest)
    {
        if (string.IsNullOrWhiteSpace(manifest.Id))
        {
            throw new InvalidDataException("Extension manifest id is required.");
        }

        if (string.IsNullOrWhiteSpace(manifest.Name))
        {
            throw new InvalidDataException("Extension manifest name is required.");
        }

        if (string.IsNullOrWhiteSpace(manifest.Version))
        {
            throw new InvalidDataException("Extension manifest version is required.");
        }

        if (string.IsNullOrWhiteSpace(manifest.EntryAssembly))
        {
            throw new InvalidDataException("Extension manifest entryAssembly is required.");
        }

        if (string.IsNullOrWhiteSpace(manifest.EntryType))
        {
            throw new InvalidDataException("Extension manifest entryType is required.");
        }

        foreach (var dependency in manifest.Dependencies)
        {
            if (string.IsNullOrWhiteSpace(dependency.Id))
            {
                throw new InvalidDataException("Extension dependency id is required.");
            }

            if (string.IsNullOrWhiteSpace(dependency.VersionRange))
            {
                throw new InvalidDataException($"Extension dependency versionRange is required for {dependency.Id}.");
            }

            if (!string.Equals(dependency.Kind, "hard", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(dependency.Kind, "soft", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException($"Extension dependency kind must be hard or soft for {dependency.Id}.");
            }
        }
    }

    private static string ComputeSha256(string packagePath)
    {
        using var stream = File.OpenRead(packagePath);
        using var sha256 = SHA256.Create();
        var hash = sha256.ComputeHash(stream);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
