using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using LMC.Extensions.Packaging;
using LMC.Extensions.Resolution;

namespace LMC.Tests.Extensions;

internal static class ExtensionPackageBuilder
{
    private static readonly JsonSerializerOptions s_serializerOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    public static ExtensionPackageInfo CreatePackageInfo(
        string id,
        string version,
        string launcherVersionRange,
        params (string Id, string VersionRange, string Kind)[] dependencies)
    {
        return new ExtensionPackageInfo
        {
            PackagePath = $"{id}.{version}.lext",
            PackageHash = $"{id}-{version}",
            Manifest = CreateManifest(
                id,
                version,
                entryAssembly: "ignored.dll",
                entryType: "Ignored.Type",
                launcherVersionRange,
                dependencies)
        };
    }

    public static ExtensionManifest CreateManifest(
        string id,
        string version,
        string entryAssembly,
        string entryType,
        string? launcherVersionRange = "[3.0.0,4.0.0)",
        params (string Id, string VersionRange, string Kind)[] dependencies)
    {
        return new ExtensionManifest
        {
            Id = id,
            Name = id,
            Version = version,
            EntryAssembly = entryAssembly,
            EntryType = entryType,
            LauncherVersionRange = launcherVersionRange,
            Dependencies = dependencies.Select(dependency => new ExtensionDependencyDescriptor
            {
                Id = dependency.Id,
                VersionRange = dependency.VersionRange,
                Kind = dependency.Kind
            }).ToList().AsReadOnly()
        };
    }

    public static string CreatePackage(
        string packagePath,
        ExtensionManifest manifest,
        Type? entryType = null,
        bool includeManifest = true,
        bool includeEntryAssembly = true,
        params (string Path, string Content)[] textEntries)
    {
        if (File.Exists(packagePath))
        {
            File.Delete(packagePath);
        }

        Directory.CreateDirectory(Path.GetDirectoryName(packagePath)!);

        using var archive = ZipFile.Open(packagePath, ZipArchiveMode.Create);

        if (includeManifest)
        {
            var manifestEntry = archive.CreateEntry("manifest.json");
            using var manifestStream = manifestEntry.Open();
            JsonSerializer.Serialize(manifestStream, manifest, s_serializerOptions);
        }

        if (includeEntryAssembly && entryType != null)
        {
            var assemblyEntry = archive.CreateEntry($"lib/{Path.GetFileName(entryType.Assembly.Location)}");
            using var sourceStream = File.OpenRead(entryType.Assembly.Location);
            using var destinationStream = assemblyEntry.Open();
            sourceStream.CopyTo(destinationStream);
        }

        foreach (var textEntry in textEntries)
        {
            var archiveEntry = archive.CreateEntry(textEntry.Path);
            using var writer = new StreamWriter(archiveEntry.Open());
            writer.Write(textEntry.Content);
        }

        return packagePath;
    }

    public static ExtensionPackageInfo CreatePackageInfo(string packagePath, ExtensionManifest manifest)
    {
        return new ExtensionPackageInfo
        {
            PackagePath = packagePath,
            PackageHash = ComputeSha256(packagePath),
            Manifest = manifest
        };
    }

    public static ResolvedExtension CreateResolvedExtension(
        ExtensionPackageInfo package,
        IReadOnlyList<ResolvedExtension>? hardDependencies = null,
        IReadOnlyList<ResolvedExtension>? softDependencies = null)
    {
        return new ResolvedExtension
        {
            Package = package,
            HardDependencies = hardDependencies ?? [],
            SoftDependencies = softDependencies ?? []
        };
    }

    private static string ComputeSha256(string packagePath)
    {
        using var stream = File.OpenRead(packagePath);
        using var sha256 = SHA256.Create();
        return Convert.ToHexString(sha256.ComputeHash(stream)).ToLowerInvariant();
    }
}
