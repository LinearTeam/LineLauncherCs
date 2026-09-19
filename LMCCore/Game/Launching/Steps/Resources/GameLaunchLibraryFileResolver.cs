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

using LMCCore.Game.Download.Installation.Forge;
using LMCCore.Game.Download.Vanilla;
using LMCCore.Game.Model.LocalVersion;
using LMCCore.Game.Model.LocalVersion.Libraries;

namespace LMCCore.Game.Launching.Steps.Resources;

internal static class GameLaunchLibraryFileResolver
{
    public static GameLaunchLibraryResolutionResult CollectMissingLibraries(
        string rootPath,
        LocalVersionInfo versionInfo,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootPath);
        ArgumentNullException.ThrowIfNull(versionInfo);

        var downloads = new Dictionary<string, GameLaunchDownloadItem>(StringComparer.OrdinalIgnoreCase);
        var blockingFailures = new List<GameLaunchLibraryBlockingFailure>();
        foreach (var library in versionInfo.Libraries)
        {
            switch (library)
            {
                case LibraryInfo detailedLibrary:
                    ProcessDetailedLibrary(rootPath, detailedLibrary, downloads, blockingFailures, cancellationToken);
                    break;
                case SimpleLibraryInfo simpleLibrary:
                    ProcessSimpleLibrary(rootPath, simpleLibrary, downloads, blockingFailures, cancellationToken);
                    break;
            }
        }

        return new GameLaunchLibraryResolutionResult(
            downloads.Values.ToList().AsReadOnly(),
            blockingFailures.AsReadOnly());
    }

    private static void ProcessDetailedLibrary(
        string rootPath,
        LibraryInfo library,
        IDictionary<string, GameLaunchDownloadItem> downloads,
        ICollection<GameLaunchLibraryBlockingFailure> blockingFailures,
        CancellationToken cancellationToken)
    {
        if (!CompatibilityRuleEvaluator.CheckRulesApply(library.Rules))
        {
            return;
        }

        if (library.Downloads?.Artifact != null)
        {
            ProcessResolvedFile(
                rootPath,
                library.Name,
                library.Downloads.Artifact,
                downloads,
                blockingFailures,
                cancellationToken);
            return;
        }

        if (!string.IsNullOrWhiteSpace(library.Url) &&
            VanillaGameDownloader.TryBuildMavenRelativePath(library.Name, out var relativePath))
        {
            var downloadUrl = new Uri(new Uri(EnsureTrailingSlash(library.Url)), relativePath).ToString();
            var savePath = ForgeLibraryPathHelper.GetAbsoluteLibraryPath(rootPath, library.Name);
            HandleRequiredFile(
                savePath,
                library.Name,
                downloadUrl,
                library.GetPreferredSha1(),
                library.Checksums,
                library.Size,
                false,
                downloads,
                blockingFailures,
                cancellationToken);
        }

        if (library.Natives is not { Count: > 0 } || library.Downloads?.Classifiers is not { Count: > 0 })
        {
            return;
        }

        var currentOs = PlatformDetector.GetCurrentOs();
        if (!library.Natives.TryGetValue(currentOs, out var nativeKey) ||
            !library.Downloads.Classifiers.TryGetValue(nativeKey, out var nativeDownload))
        {
            return;
        }

        ProcessResolvedFile(
            rootPath,
            $"{library.Name}:{nativeKey}",
            nativeDownload,
            downloads,
            blockingFailures,
            cancellationToken);
    }

    private static void ProcessSimpleLibrary(
        string rootPath,
        SimpleLibraryInfo library,
        IDictionary<string, GameLaunchDownloadItem> downloads,
        ICollection<GameLaunchLibraryBlockingFailure> blockingFailures,
        CancellationToken cancellationToken)
    {
        if (!VanillaGameDownloader.TryBuildMavenRelativePath(library.Name, out var relativePath))
        {
            return;
        }

        var hasExplicitUrl = !string.IsNullOrWhiteSpace(library.Url);
        var baseUrl = hasExplicitUrl ? library.Url! : VanillaGameDownloader.OfficialLibraryBaseUrl;
        var downloadUrl = new Uri(new Uri(EnsureTrailingSlash(baseUrl)), relativePath).ToString();
        var savePath = ForgeLibraryPathHelper.GetAbsoluteLibraryPath(rootPath, library.Name);
        HandleRequiredFile(
            savePath,
            library.Name,
            downloadUrl,
            library.GetPreferredSha1(),
            library.Checksums,
            library.Size,
            !hasExplicitUrl,
            downloads,
            blockingFailures,
            cancellationToken);
    }

    private static void ProcessResolvedFile(
        string rootPath,
        string displayName,
        DownloadableFileInfo file,
        IDictionary<string, GameLaunchDownloadItem> downloads,
        ICollection<GameLaunchLibraryBlockingFailure> blockingFailures,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(file.Path))
        {
            throw new InvalidOperationException($"Library '{displayName}' is missing path metadata.");
        }

        var savePath = VanillaGameDownloader.GetLibrarySavePath(rootPath, file.Path);
        HandleRequiredFile(
            savePath,
            displayName,
            file.Url,
            file.Sha1,
            file.Checksums,
            file.Size,
            file.IgnoreNotFound,
            downloads,
            blockingFailures,
            cancellationToken);
    }

    private static void HandleRequiredFile(
        string savePath,
        string displayName,
        string? downloadUrl,
        string? sha1,
        IReadOnlyList<string>? hashes,
        long? size,
        bool ignoreNotFound,
        IDictionary<string, GameLaunchDownloadItem> downloads,
        ICollection<GameLaunchLibraryBlockingFailure> blockingFailures,
        CancellationToken cancellationToken)
    {
        var check = GameLaunchFileIntegrityHelper.CheckFile(savePath, sha1, hashes, size, cancellationToken);
        if (check.IsValid)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(downloadUrl))
        {
            blockingFailures.Add(new GameLaunchLibraryBlockingFailure(displayName, savePath, check.Reason));
            return;
        }

        downloads[savePath] = new GameLaunchDownloadItem(
            savePath,
            downloadUrl,
            displayName,
            size,
            sha1,
            hashes,
            ignoreNotFound);
    }

    private static string EnsureTrailingSlash(string url)
    {
        return url.EndsWith("/", StringComparison.Ordinal) ? url : $"{url}/";
    }
}

internal sealed record GameLaunchLibraryResolutionResult(
    IReadOnlyList<GameLaunchDownloadItem> Downloads,
    IReadOnlyList<GameLaunchLibraryBlockingFailure> BlockingFailures);

internal sealed record GameLaunchLibraryBlockingFailure(
    string DisplayName,
    string SavePath,
    string Reason);
