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
    public static IReadOnlyList<GameLaunchDownloadItem> CollectMissingLibraryDownloads(
        string rootPath,
        LocalVersionInfo versionInfo,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootPath);
        ArgumentNullException.ThrowIfNull(versionInfo);

        var downloads = new Dictionary<string, GameLaunchDownloadItem>(StringComparer.OrdinalIgnoreCase);
        foreach (var library in versionInfo.Libraries)
        {
            switch (library)
            {
                case LibraryInfo detailedLibrary:
                    ProcessDetailedLibrary(rootPath, detailedLibrary, downloads, cancellationToken);
                    break;
                case SimpleLibraryInfo simpleLibrary:
                    ProcessSimpleLibrary(rootPath, simpleLibrary, downloads, cancellationToken);
                    break;
            }
        }

        return downloads.Values.ToList().AsReadOnly();
    }

    private static void ProcessDetailedLibrary(
        string rootPath,
        LibraryInfo library,
        IDictionary<string, GameLaunchDownloadItem> downloads,
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
            cancellationToken);
    }

    private static void ProcessSimpleLibrary(
        string rootPath,
        SimpleLibraryInfo library,
        IDictionary<string, GameLaunchDownloadItem> downloads,
        CancellationToken cancellationToken)
    {
        var savePath = ForgeLibraryPathHelper.GetAbsoluteLibraryPath(rootPath, library.Name);
        HandleRequiredFile(
            savePath,
            library.Name,
            library.Url,
            library.Sha1,
            library.Size,
            downloads,
            cancellationToken);
    }

    private static void ProcessResolvedFile(
        string rootPath,
        string displayName,
        DownloadableFileInfo file,
        IDictionary<string, GameLaunchDownloadItem> downloads,
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
            file.Size,
            downloads,
            cancellationToken);
    }

    private static void HandleRequiredFile(
        string savePath,
        string displayName,
        string? downloadUrl,
        string? sha1,
        long? size,
        IDictionary<string, GameLaunchDownloadItem> downloads,
        CancellationToken cancellationToken)
    {
        var check = GameLaunchFileIntegrityHelper.CheckFile(savePath, sha1, size, cancellationToken);
        if (check.IsValid)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(downloadUrl))
        {
            throw new InvalidOperationException(
                $"Library '{displayName}' failed validation and cannot be downloaded: {check.Reason}");
        }

        downloads[savePath] = new GameLaunchDownloadItem(
            savePath,
            downloadUrl,
            displayName,
            size,
            sha1);
    }
}
