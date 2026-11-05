// Copyright 2025-2026 LinearTeam
// 
//    Licensed under the Apache License, Version 2.0 (the "License");
//    you may not use this file except in compliance with the License.
//    You may obtain a copy of the License at
// 
//        http://www.apache.org/licenses/LICENSE-2.0
// 
//    Unless required by applicable law or agreed to in writing, software
//    distributed under the License is distributed on an "AS IS" BASIS,
//    WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//    See the License for the specific language governing permissions and
//    limitations under the License.

using LMC;
using LMC.Basic.Logging;
using LMCCore.Game.Download.Vanilla;
using LMCCore.Game.Launching.Steps.Resources;
using LMCCore.Game.Model.LocalVersion;
using LMCCore.Game.Versioning;

namespace LMCCore.Game.Download.Installation.Caching;

internal static class GameInstallationLocalReuseHelper
{
    private readonly static Logger s_logger = new("GameInstall.LocalReuse");
    private static readonly TimeSpan s_fabricApiReuseBudget = TimeSpan.FromMilliseconds(200);
    private const int FabricApiMaxPaths = 128;
    private const int FabricApiMaxHashCandidates = 4;

    public static Task<bool> TryPopulateFabricApiFromLocalModsAsync(
        string currentRootPath,
        string targetPath,
        string fileName,
        string? sha1,
        long? size,
        CancellationToken cancellationToken)
        => Task.Run(async () =>
        {
            using var budget = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            budget.CancelAfter(s_fabricApiReuseBudget);
            try
            {
                budget.Token.ThrowIfCancellationRequested();
                if (string.IsNullOrWhiteSpace(sha1) || size is null or < 0 ||
                    string.IsNullOrWhiteSpace(fileName) || Path.GetFileName(fileName) != fileName ||
                    fileName.Contains('\\') || !fileName.EndsWith(".jar", StringComparison.OrdinalIgnoreCase))
                    return false;

                var hashesComputed = 0;
                foreach (var candidate in EnumerateFabricApiCandidates(currentRootPath, fileName, budget.Token))
                {
                    budget.Token.ThrowIfCancellationRequested();
                    try
                    {
                        if (!File.Exists(candidate) || !string.Equals(Path.GetFileName(candidate), fileName,
                                StringComparison.Ordinal) || new FileInfo(candidate).Length != size ||
                            (File.GetAttributes(candidate) & FileAttributes.ReparsePoint) != 0)
                            continue;
                        if (hashesComputed++ >= FabricApiMaxHashCandidates)
                            return false;
                        await using var stream = new FileStream(candidate, FileMode.Open, FileAccess.Read,
                            FileShare.Read, 81920, FileOptions.Asynchronous | FileOptions.SequentialScan);
                        var hash = Convert.ToHexStringLower(await System.Security.Cryptography.SHA1.HashDataAsync(
                            stream, budget.Token).ConfigureAwait(false));
                        if (!string.Equals(hash, sha1, StringComparison.OrdinalIgnoreCase))
                            continue;
                        budget.Token.ThrowIfCancellationRequested();
                        stream.Position = 0;
                        await CopyAtomicallyAsync(stream, targetPath, budget.Token).ConfigureAwait(false);
                        s_logger.Info($"Reused Fabric API from '{candidate}' to '{targetPath}'.");
                        return true;
                    }
                    catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
                    {
                        // Local reuse is best-effort; installation downloads inaccessible artifacts instead.
                    }
                }
            }
            catch (OperationCanceledException)
            {
                cancellationToken.ThrowIfCancellationRequested();
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
            {
                // A missing or inaccessible root must not prevent the normal download path.
            }
            cancellationToken.ThrowIfCancellationRequested();
            return false;
        }, cancellationToken);

    private static IEnumerable<string> EnumerateFabricApiCandidates(
        string currentRootPath, string fileName, CancellationToken token)
    {
        var roots = new[] { currentRootPath }.Concat(Current.Config?.ManagedGameRootPaths.ToArray() ?? []);
        var seenRoots = new HashSet<string>(OperatingSystem.IsWindows()
            ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);
        var versionsVisited = 0;
        foreach (var root in roots)
        {
            token.ThrowIfCancellationRequested();
            IEnumerator<string> versions;
            try
            {
                if (string.IsNullOrWhiteSpace(root) || !seenRoots.Add(NormalizePath(root)))
                    continue;
                versions = Directory.EnumerateDirectories(Path.Combine(root, "versions")).GetEnumerator();
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
            { continue; }
            using (versions)
            {
                while (versionsVisited < FabricApiMaxPaths)
                {
                    token.ThrowIfCancellationRequested();
                    string version;
                    try
                    {
                        // Enumeration may fail on MoveNext, after the enumerator has been created.
                        if (!versions.MoveNext())
                            break;
                        version = versions.Current;
                    }
                    catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                    { break; }
                    token.ThrowIfCancellationRequested();
                    versionsVisited++;
                    yield return Path.Combine(version, "mods", fileName);
                }
            }
            if (versionsVisited >= FabricApiMaxPaths)
                yield break;
        }
    }

    private static async Task CopyAtomicallyAsync(Stream source, string targetPath, CancellationToken token)
    {
        var directory = Path.GetDirectoryName(targetPath)
                        ?? throw new InvalidOperationException("Failed to resolve destination directory.");
        Directory.CreateDirectory(directory);
        var temporary = Path.Combine(directory, $".fabric-api-{Guid.NewGuid():N}.tmp");
        try
        {
            await using (var target = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None,
                             81920, FileOptions.Asynchronous | FileOptions.SequentialScan))
                await source.CopyToAsync(target, token).ConfigureAwait(false);
            token.ThrowIfCancellationRequested();
            File.Move(temporary, targetPath, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporary)) File.Delete(temporary);
        }
    }

    public static Task<bool> TryPopulateClientJarFromKnownVersionsAsync(
        string currentRootPath,
        string targetPath,
        string? sha1,
        long? size,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(currentRootPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(targetPath);

        return TryCopyFirstValidCandidateAsync(
            targetPath,
            EnumerateVersionJarCandidates(currentRootPath),
            ".jar",
            null,
            sha1,
            null,
            size,
            "vanilla client jar",
            cancellationToken);
    }

    public static Task<bool> TryPopulateOptiFineInstallerFromKnownVersionsAsync(
        string currentRootPath,
        string targetPath,
        string installerFileName,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(currentRootPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(targetPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(installerFileName);

        return TryCopyFirstValidCandidateAsync(
            targetPath,
            EnumerateOptiFineInstallerCandidates(currentRootPath, installerFileName),
            ".jar",
            installerFileName,
            null,
            null,
            null,
            "OptiFine installer",
            cancellationToken);
    }

    public static async Task WarmLibrariesFromManagedRootsAsync(
        string currentRootPath,
        IReadOnlyCollection<DownloadableFileInfo> libraries,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(currentRootPath);
        ArgumentNullException.ThrowIfNull(libraries);

        foreach (var library in libraries)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (string.IsNullOrWhiteSpace(library.Path))
            {
                continue;
            }

            var targetPath = VanillaGameDownloader.GetLibrarySavePath(currentRootPath, library.Path);
            var size = library.Size is > 0 ? library.Size : null;
            if (GameLaunchFileIntegrityHelper.CheckFile(targetPath, library.Sha1, library.Checksums, size, cancellationToken).IsValid)
            {
                continue;
            }

            await TryCopyFromManagedRootsAsync(
                currentRootPath,
                targetPath,
                Path.Combine("libraries", library.Path),
                Path.GetFileName(library.Path),
                library.Sha1,
                library.Checksums,
                size,
                "library",
                cancellationToken);
        }
    }

    public static async Task WarmAssetsFromManagedRootsAsync(
        string currentRootPath,
        IReadOnlyDictionary<string, AssetInfo> assets,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(currentRootPath);
        ArgumentNullException.ThrowIfNull(assets);

        var assetRoot = Path.Combine(currentRootPath, "assets");
        foreach (var asset in assets.Values)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var targetPath = VanillaGameDownloader.GetAssetSavePath(assetRoot, asset.Hash);
            long? size = asset.Size > 0 ? asset.Size : null;
            if (GameLaunchFileIntegrityHelper.CheckFile(targetPath, asset.Hash, size, cancellationToken).IsValid)
            {
                continue;
            }

            await TryCopyFromManagedRootsAsync(
                currentRootPath,
                targetPath,
                Path.Combine("assets", "objects", asset.Hash[..2], asset.Hash),
                Path.GetFileName(targetPath),
                asset.Hash,
                null,
                size,
                "asset",
                cancellationToken);
        }
    }

    public static Task<bool> TryPopulateAssetIndexFromManagedRootsAsync(
        string currentRootPath,
        AssetIndexInfo assetIndex,
        string targetPath,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(currentRootPath);
        ArgumentNullException.ThrowIfNull(assetIndex);
        ArgumentException.ThrowIfNullOrWhiteSpace(targetPath);

        long? size = assetIndex.Size > 0 ? assetIndex.Size : null;
        var fileName = $"{assetIndex.Id}.json";
        return TryCopyFromManagedRootsAsync(
            currentRootPath,
            targetPath,
            Path.Combine("assets", "indexes", fileName),
            fileName,
            assetIndex.Sha1,
            null,
            size,
            "asset index",
            cancellationToken);
    }

    private static IEnumerable<string> EnumerateVersionJarCandidates(string currentRootPath)
    {
        foreach (var versionsDirectory in EnumerateVersionDirectories(currentRootPath))
        {
            var versionName = Path.GetFileName(versionsDirectory);
            if (string.IsNullOrWhiteSpace(versionName))
            {
                continue;
            }

            yield return Path.Combine(versionsDirectory, $"{versionName}.jar");
        }
    }

    private static IEnumerable<string> EnumerateOptiFineInstallerCandidates(string currentRootPath, string installerFileName)
    {
        foreach (var versionsDirectory in EnumerateVersionDirectories(currentRootPath))
        {
            yield return Path.Combine(versionsDirectory, installerFileName);
            yield return Path.Combine(versionsDirectory, "mods", installerFileName);
        }
    }

    private static IEnumerable<string> EnumerateVersionDirectories(string currentRootPath)
    {
        foreach (var rootPath in GetKnownGameRoots(currentRootPath))
        {
            var versionsPath = Path.Combine(rootPath, "versions");
            if (!Directory.Exists(versionsPath))
            {
                continue;
            }

            foreach (var versionDirectory in Directory.EnumerateDirectories(versionsPath))
            {
                yield return versionDirectory;
            }
        }
    }

    private static async Task<bool> TryCopyFromManagedRootsAsync(
        string currentRootPath,
        string targetPath,
        string relativePath,
        string? expectedFileName,
        string? sha1,
        IReadOnlyList<string>? hashes,
        long? size,
        string description,
        CancellationToken cancellationToken)
    {
        foreach (var rootPath in GetOtherKnownGameRoots(currentRootPath))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var candidatePath = Path.Combine(rootPath, relativePath);
            if (!MatchesBasicConstraints(candidatePath, expectedFileName, Path.GetExtension(targetPath), size))
            {
                continue;
            }

            var check = GameLaunchFileIntegrityHelper.CheckFile(candidatePath, sha1, hashes, size, cancellationToken);
            if (!check.IsValid)
            {
                continue;
            }

            await CopyFileAsync(candidatePath, targetPath, cancellationToken);
            s_logger.Info($"Reused {description} from '{candidatePath}' to '{targetPath}'.");
            return true;
        }

        return false;
    }

    private static async Task<bool> TryCopyFirstValidCandidateAsync(
        string targetPath,
        IEnumerable<string> candidatePaths,
        string requiredExtension,
        string? expectedFileName,
        string? sha1,
        IReadOnlyList<string>? hashes,
        long? size,
        string description,
        CancellationToken cancellationToken)
    {
        foreach (var candidatePath in candidatePaths)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!MatchesBasicConstraints(candidatePath, expectedFileName, requiredExtension, size))
            {
                continue;
            }

            var check = GameLaunchFileIntegrityHelper.CheckFile(candidatePath, sha1, hashes, size, cancellationToken);
            if (!check.IsValid)
            {
                continue;
            }

            await CopyFileAsync(candidatePath, targetPath, cancellationToken);
            s_logger.Info($"Reused {description} from '{candidatePath}' to '{targetPath}'.");
            return true;
        }

        return false;
    }

    private static bool MatchesBasicConstraints(
        string candidatePath,
        string? expectedFileName,
        string requiredExtension,
        long? size)
    {
        if (!File.Exists(candidatePath))
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(expectedFileName) &&
            !string.Equals(Path.GetFileName(candidatePath), expectedFileName, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(requiredExtension) &&
            !string.Equals(Path.GetExtension(candidatePath), requiredExtension, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return !size.HasValue || new FileInfo(candidatePath).Length == size.Value;
    }

    private static IReadOnlyList<string> GetKnownGameRoots(string currentRootPath)
    {
        var roots = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        AddNormalizedRoot(roots, currentRootPath);

        var configRoots = Current.Config?.ManagedGameRootPaths;
        if (configRoots != null)
        {
            foreach (var rootPath in configRoots)
            {
                AddNormalizedRoot(roots, rootPath);
            }
        }

        return roots.ToList().AsReadOnly();
    }

    private static IEnumerable<string> GetOtherKnownGameRoots(string currentRootPath)
    {
        var normalizedCurrentRoot = NormalizePath(currentRootPath);
        return GetKnownGameRoots(currentRootPath)
            .Where(rootPath => !string.Equals(rootPath, normalizedCurrentRoot, StringComparison.OrdinalIgnoreCase));
    }

    private static void AddNormalizedRoot(ISet<string> roots, string? rootPath)
    {
        if (string.IsNullOrWhiteSpace(rootPath))
        {
            return;
        }

        var normalizedRoot = NormalizePath(rootPath);
        if (Directory.Exists(normalizedRoot))
        {
            roots.Add(normalizedRoot);
        }
    }

    private static string NormalizePath(string path)
    {
        return VersionPathUtils.NormalizePath(path);
    }

    private static async Task CopyFileAsync(string sourcePath, string destinationPath, CancellationToken cancellationToken)
    {
        var destinationDirectory = Path.GetDirectoryName(destinationPath)
                                   ?? throw new InvalidOperationException("Failed to resolve destination directory.");
        Directory.CreateDirectory(destinationDirectory);

        await using var sourceStream = new FileStream(sourcePath, new FileStreamOptions
        {
            Access = FileAccess.Read,
            Mode = FileMode.Open,
            Share = FileShare.Read,
            Options = FileOptions.Asynchronous | FileOptions.SequentialScan
        });
        await using var destinationStream = new FileStream(destinationPath, new FileStreamOptions
        {
            Access = FileAccess.Write,
            Mode = FileMode.Create,
            Share = FileShare.None,
            Options = FileOptions.Asynchronous | FileOptions.SequentialScan
        });

        await sourceStream.CopyToAsync(destinationStream, cancellationToken);
    }
}
