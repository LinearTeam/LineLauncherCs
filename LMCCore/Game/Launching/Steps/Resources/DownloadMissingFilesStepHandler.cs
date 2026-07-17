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

using LMC.Basic.Logging;
using LMCCore.Game.Download.Installation.Caching;
using LMCCore.Game.Download.Vanilla;
using LMCCore.Game.Download.Vanilla.Batching;
using LMCCore.Game.Launching.Execution;
using LMCCore.Game.Model.LocalVersion;

namespace LMCCore.Game.Launching.Steps.Resources;

public sealed class DownloadMissingFilesStepHandler : IGameLaunchStepHandler
{
    private readonly static Logger s_logger = new("LaunchDownloadFiles");

    public GameLaunchProgressStep Step => GameLaunchProgressStep.DownloadMissingFiles;

    public async Task ExecuteAsync(GameLaunchContext context, CancellationToken cancellationToken)
    {
        if (!context.ShouldValidateAndCompleteMissingFiles)
        {
            s_logger.Info($"Skipping launch file validation for '{context.Version.VersionName}'.");
            return;
        }

        var versionInfo = context.Version.VersionInfo
                          ?? throw new InvalidOperationException("Version info is required before launching.");
        var pendingDownloads = new Dictionary<string, GameLaunchDownloadItem>(StringComparer.OrdinalIgnoreCase);

        await CollectClientJarDownloadAsync(context, versionInfo, pendingDownloads, cancellationToken);
        await EnsureAssetIndexAndCollectAssetsAsync(context, versionInfo, pendingDownloads, cancellationToken);
        await WarmLibrariesAsync(context, versionInfo, cancellationToken);

        var libraryResolution = GameLaunchLibraryFileResolver.CollectMissingLibraries(
            context.Version.RootPath,
            versionInfo,
            cancellationToken);
        foreach (var libraryDownload in libraryResolution.Downloads)
        {
            pendingDownloads[libraryDownload.SavePath] = libraryDownload;
        }

        if (pendingDownloads.Count == 0)
        {
            ThrowIfBlockingLibrariesExist(context, libraryResolution.BlockingFailures);
            s_logger.Info($"No missing launch files were detected for '{context.Version.VersionName}'.");
            return;
        }

        foreach (var download in pendingDownloads.Values.OrderBy(item => item.SavePath, StringComparer.OrdinalIgnoreCase))
        {
            s_logger.Info($"Missing file queued: {download.DisplayName} -> {download.SavePath}");
        }

        var batchResult = await BatchDownloader.DownloadAsync(
            new BatchDownloadOptions<GameLaunchDownloadItem>
            {
                Files = pendingDownloads.Values,
                MaxConcurrency = 8,
                MaxRetries = 3,
                GetSavePath = item => item.SavePath,
                GetDownloadUrl = item => item.DownloadUrl,
                GetFileSize = item => item.Size,
                GetHash = item => item.Hash,
                GetHashes = item => item.Hashes,
                GetIgnoreNotFound = item => item.IgnoreNotFound,
                GetDisplayName = item => item.DisplayName,
                SkipIfSizeMatches = true,
                SkipIfHashMatches = true
            },
            cancellationToken);

        if (batchResult.FailedCount > 0)
        {
            throw new InvalidOperationException(
                $"Failed to download {batchResult.FailedCount} launch file(s) for '{context.Version.VersionName}'.");
        }

        ThrowIfBlockingLibrariesExist(context, libraryResolution.BlockingFailures);

        s_logger.Info(
            $"Launch file repair completed for '{context.Version.VersionName}': downloaded {batchResult.SuccessCount}, skipped {batchResult.SkippedCount}.");
    }

    private static void ThrowIfBlockingLibrariesExist(
        GameLaunchContext context,
        IReadOnlyList<GameLaunchLibraryBlockingFailure> blockingFailures)
    {
        if (blockingFailures.Count == 0)
        {
            return;
        }

        foreach (var failure in blockingFailures)
        {
            s_logger.Error(
                $"Required library '{failure.DisplayName}' is missing and has no download source. Path: '{failure.SavePath}'. Reason: {failure.Reason}");
        }

        var libraryNames = string.Join(", ", blockingFailures.Select(failure => failure.DisplayName));
        throw new InvalidOperationException(
            $"Required libraries are missing and cannot be downloaded for '{context.Version.VersionName}': {libraryNames}");
    }

    private static async Task CollectClientJarDownloadAsync(
        GameLaunchContext context,
        LocalVersionInfo versionInfo,
        IDictionary<string, GameLaunchDownloadItem> pendingDownloads,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(context.Version.JarPath))
        {
            throw new InvalidOperationException("Version jar path is missing.");
        }

        if (versionInfo.Downloads == null ||
            !versionInfo.Downloads.TryGetValue("client", out var clientDownload) ||
            clientDownload == null)
        {
            throw new InvalidOperationException("Version client download metadata is missing.");
        }

        var jarCheck = GameLaunchFileIntegrityHelper.CheckFile(
            context.Version.JarPath,
            clientDownload.Sha1,
            clientDownload.Size,
            cancellationToken);
        if (jarCheck.IsValid)
        {
            return;
        }

        await GameInstallationLocalReuseHelper.TryPopulateClientJarFromKnownVersionsAsync(
            context.Version.RootPath,
            context.Version.JarPath,
            clientDownload.Sha1,
            clientDownload.Size,
            cancellationToken);
        jarCheck = GameLaunchFileIntegrityHelper.CheckFile(
            context.Version.JarPath,
            clientDownload.Sha1,
            clientDownload.Size,
            cancellationToken);
        if (jarCheck.IsValid)
        {
            return;
        }

        throw new InvalidOperationException(
            $"Version jar is invalid for '{context.Version.JarPath}': {jarCheck.Reason}");
    }

    async private Task EnsureAssetIndexAndCollectAssetsAsync(
        GameLaunchContext context,
        LocalVersionInfo versionInfo,
        IDictionary<string, GameLaunchDownloadItem> pendingDownloads,
        CancellationToken cancellationToken)
    {
        if (versionInfo.AssetIndex == null)
        {
            return;
        }

        var assetRoot = Path.Combine(context.Version.RootPath, "assets");
        var assetIndexJson = await TryGetValidAssetIndexJsonAsync(context, versionInfo.AssetIndex, assetRoot, cancellationToken);
        if (string.IsNullOrWhiteSpace(assetIndexJson))
        {
            return;
        }

        var assets = context.VanillaDownloader.ParseAssetIndex(assetIndexJson);
        await GameInstallationLocalReuseHelper.WarmAssetsFromManagedRootsAsync(
            context.Version.RootPath,
            assets,
            cancellationToken);
        foreach (var asset in assets)
        {
            var savePath = VanillaGameDownloader.GetAssetSavePath(assetRoot, asset.Value.Hash);
            var check = GameLaunchFileIntegrityHelper.CheckFile(
                savePath,
                asset.Value.Hash,
                asset.Value.Size > 0 ? asset.Value.Size : null,
                cancellationToken);
            if (check.IsValid)
            {
                continue;
            }

            pendingDownloads[savePath] = new GameLaunchDownloadItem(
                savePath,
                context.VanillaDownloader.GetAssetDownloadUrl(asset.Value.Hash),
                asset.Key,
                asset.Value.Size > 0 ? asset.Value.Size : null,
                asset.Value.Hash);
        }
    }

    async private Task<string?> TryGetValidAssetIndexJsonAsync(
        GameLaunchContext context,
        AssetIndexInfo assetIndex,
        string assetRoot,
        CancellationToken cancellationToken)
    {
        var indexesDirectory = Path.Combine(assetRoot, "indexes");
        Directory.CreateDirectory(indexesDirectory);
        var assetIndexPath = Path.Combine(indexesDirectory, $"{assetIndex.Id}.json");

        var localCheck = GameLaunchFileIntegrityHelper.CheckFile(
            assetIndexPath,
            assetIndex.Sha1,
            assetIndex.Size > 0 ? assetIndex.Size : null,
            cancellationToken);
        if (localCheck.IsValid)
        {
            return await File.ReadAllTextAsync(assetIndexPath, cancellationToken);
        }

        await GameInstallationLocalReuseHelper.TryPopulateAssetIndexFromManagedRootsAsync(
            context.Version.RootPath,
            assetIndex,
            assetIndexPath,
            cancellationToken);
        localCheck = GameLaunchFileIntegrityHelper.CheckFile(
            assetIndexPath,
            assetIndex.Sha1,
            assetIndex.Size > 0 ? assetIndex.Size : null,
            cancellationToken);
        if (localCheck.IsValid)
        {
            return await File.ReadAllTextAsync(assetIndexPath, cancellationToken);
        }

        if (string.IsNullOrWhiteSpace(assetIndex.Url))
        {
            s_logger.Warn($"Asset index '{assetIndex.Id}' is unavailable and no download url exists: {localCheck.Reason}");
            return null;
        }

        s_logger.Warn($"Asset index '{assetIndex.Id}' failed validation, re-downloading: {localCheck.Reason}");
        try
        {
            var transformedUrl = context.DownloadSourceManager.TransformUrl(assetIndex.Url) ?? assetIndex.Url;
            await VanillaGameSubTaskFactory.DownloadSingleFileAsync(
                transformedUrl,
                assetIndexPath,
                assetIndex.Sha1,
                assetIndex.Size > 0 ? assetIndex.Size : null,
                cancellationToken);
        }
        catch (Exception ex)
        {
            s_logger.Warn($"Asset index '{assetIndex.Id}' re-download failed but launch may continue: {ex.Message}");
            return null;
        }

        var downloadedCheck = GameLaunchFileIntegrityHelper.CheckFile(
            assetIndexPath,
            assetIndex.Sha1,
            assetIndex.Size > 0 ? assetIndex.Size : null,
            cancellationToken);
        if (!downloadedCheck.IsValid)
        {
            s_logger.Warn(
                $"Asset index '{assetIndex.Id}' is still invalid after re-download but launch may continue: {downloadedCheck.Reason}");
            return null;
        }

        return await File.ReadAllTextAsync(assetIndexPath, cancellationToken);
    }

    private static async Task WarmLibrariesAsync(
        GameLaunchContext context,
        LocalVersionInfo versionInfo,
        CancellationToken cancellationToken)
    {
        var libraries = VanillaGameDownloader.GetLibrariesForDownload(versionInfo);
        if (libraries.Count == 0)
        {
            return;
        }

        await GameInstallationLocalReuseHelper.WarmLibrariesFromManagedRootsAsync(
            context.Version.RootPath,
            libraries,
            cancellationToken);
    }
}
