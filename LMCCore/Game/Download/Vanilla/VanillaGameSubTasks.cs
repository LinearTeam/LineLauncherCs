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

using LMCCore.Tasks.Model;
using LMC.Basic.Logging;
using LMCCore.Game.Model.LocalVersion;

namespace LMCCore.Game.Download.Vanilla;

public static class VanillaGameSubTaskFactory
{
    private readonly static Logger s_logger = new("Download.Vanilla");

    /// <summary>
    /// 创建依赖库下载子任务执行器
    /// </summary>
    public static Func<CancellationToken, Dictionary<SubTaskBase, object>, IProgress<int>, Task<List<DownloadableFileInfo>>>
        CreateLibrariesExecutor(VanillaGameDownloader downloader, List<DownloadableFileInfo> libraries, string libraryRoot, int maxConcurrency = 8)
    {
        return async (cancellationToken, _, progress) =>
        {
            s_logger.Info($"开始下载 {libraries.Count} 个依赖库");

            if (libraries.Count == 0)
            {
                s_logger.Info("未找到可用的依赖库");
                return [];
            }

            var downloadedLibraries = new List<DownloadableFileInfo>();

            var result = await BatchDownloader.DownloadAsync(
                new BatchDownloadOptions<DownloadableFileInfo>
                {
                    Files = libraries,
                    MaxConcurrency = maxConcurrency,
                    MaxRetries = 3,
                    GetSavePath = lib => VanillaGameDownloader.GetLibrarySavePath(libraryRoot, lib.Path!),
                    GetDownloadUrl = lib => downloader.GetLibraryDownloadUrl(lib.Url!),
                    GetFileSize = lib => lib.Size > 0 ? lib.Size : null,
                    GetHash = lib => lib.Sha1,
                    GetDisplayName = lib => lib.Path,
                    SkipIfSizeMatches = true,
                    SkipIfHashMatches = true
                },
                cancellationToken,
                progress);

            // 记录下载结果（重新扫描以获取成功下载的文件）
            // 由于BatchDownloader不保留下载记录，我们需要重新检查
            s_logger.Info($"依赖库下载结束 {result.SuccessCount} 成功, {result.FailedCount} 失败, {result.SkippedCount} 跳过");
            return downloadedLibraries;
        };
    }
    
    /// <summary>
    /// 创建资源文件下载子任务执行器
    /// </summary>
    public static Func<CancellationToken, Dictionary<SubTaskBase, object>, IProgress<int>, Task<Dictionary<string, AssetInfo>>>
        CreateAssetsExecutor(VanillaGameDownloader downloader, string versionId, AssetIndexInfo? assetIndex, string assetRoot, int maxConcurrency = 16)
    {
        return async (cancellationToken, _, progress) =>
        {
            var downloadedAssets = new Dictionary<string, AssetInfo>();

            s_logger.Info($"正在获取版本 {versionId} 的资源文件索引");
            var assetIndexJson = await downloader.GetAssetIndexJsonAsync(assetIndex, cancellationToken);

            // 保存asset index JSON到 assets/indexes/<id>.json
            var assetIndexesDir = Path.Combine(assetRoot, "indexes");
            Directory.CreateDirectory(assetIndexesDir);
            var assetIndexPath = Path.Combine(assetIndexesDir, $"{assetIndex?.Id}.json");
            await File.WriteAllTextAsync(assetIndexPath, assetIndexJson, cancellationToken);
            s_logger.Info($"资源文件索引已保存至 {assetIndexPath}");

            var assets = downloader.ParseAssetIndex(assetIndexJson);
            s_logger.Info($"开始下载 {assets.Count} 个资源文件");

            if (assets.Count == 0)
            {
                s_logger.Info("没有可下载的资源文件");
                return downloadedAssets;
            }

            // 将字典转换为列表以便批量下载
            var assetList = assets.ToList();
            var result = await BatchDownloader.DownloadAsync(
                new BatchDownloadOptions<KeyValuePair<string, AssetInfo>>
                {
                    Files = assetList,
                    MaxConcurrency = maxConcurrency,
                    MaxRetries = 1, // 资源文件下载失败重试次数较少
                    GetSavePath = kvp => VanillaGameDownloader.GetAssetSavePath(assetRoot, kvp.Value.Hash),
                    GetDownloadUrl = kvp => downloader.GetAssetDownloadUrl(kvp.Value.Hash),
                    GetFileSize = kvp => kvp.Value.Size,
                    GetHash = kvp => kvp.Value.Hash,
                    GetDisplayName = kvp => kvp.Key,
                    SkipIfSizeMatches = true
                },
                cancellationToken,
                progress);

            // 统计下载结果
            s_logger.Info($"资源文件下载完成， {result.SuccessCount} 成功 {result.FailedCount} 失败 {result.SkippedCount} 跳过");
            return downloadedAssets;
        };
    }
}
