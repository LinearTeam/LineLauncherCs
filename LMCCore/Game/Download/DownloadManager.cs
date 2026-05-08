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

using LMCCore.Game.Download.Model.Vanilla;
using LMCCore.Game.Download.Vanilla;
using LMCCore.Game.Model.LocalVersion;
using LMCCore.Tasks.Model;

namespace LMCCore.Game.Download;

public class DownloadManager
{
    private DownloadSourceManager _downloadSourceManager = DownloadSourceManager.CreateDefault();
    private VanillaGameDownloader? _vanillaDownloader;

    public DownloadManager()
    {
    }

    public DownloadManager(DownloadSourceManager sourceManager)
    {
        _downloadSourceManager = sourceManager ?? throw new ArgumentNullException(nameof(sourceManager));
    }

    private VanillaGameDownloader VanillaDownloader =>
        _vanillaDownloader ??= new VanillaGameDownloader(_downloadSourceManager);

    public (SubTask<List<DownloadableFileInfo>> LibrariesTask, SubTask<Dictionary<string, AssetInfo>> AssetsTask)
        CreateVanillaGameSubTasks(
            ParentTask parent,
            LocalVersionInfo versionInfo,
            string libraryRoot,
            string assetRoot,
            int librariesPriority = 50,
            int assetsPriority = 50)
    {
        var librariesTask = CreateLibrariesSubTask(
            parent, VanillaGameDownloader.GetLibrariesForDownload(versionInfo), libraryRoot, librariesPriority);

        var assetsTask = CreateAssetsSubTask(
            parent, versionInfo, assetRoot, assetsPriority);

        return (librariesTask, assetsTask);
    }

    public SubTask<List<DownloadableFileInfo>> CreateLibrariesSubTask(
        ParentTask parent,
        List<DownloadableFileInfo> libraries,
        string libraryRoot,
        int priority = 100)
    {
        var name = $"下载原版依赖库";
        var executor = VanillaGameSubTaskFactory.CreateLibrariesExecutor(
            VanillaDownloader,libraries , libraryRoot);

        return parent.CreateSubTask(name, priority, executor);
    }

    public SubTask<Dictionary<string, AssetInfo>> CreateAssetsSubTask(
        ParentTask parent,
        LocalVersionInfo versionInfo,
        string assetRoot,
        int priority = 50)
    {
        var name = $"下载原版资源文件";
        var executor = VanillaGameSubTaskFactory.CreateAssetsExecutor(
            VanillaDownloader, versionInfo.Id, versionInfo.AssetIndex, assetRoot);

        return parent.CreateSubTask(name, priority, executor);
    }

    /// <summary>
    /// 获取版本列表
    /// </summary>
    public async Task<VersionManifestInfo> GetVersionManifestAsync(CancellationToken cancellationToken = default)
    {
        return await VanillaDownloader.GetVersionManifestAsync(cancellationToken);
    }

    /// <summary>
    /// 获取指定版本的版本信息
    /// </summary>
    public async Task<LocalVersionInfo?> GetVersionInfoAsync(string versionId, CancellationToken cancellationToken = default)
    {
        return await VanillaDownloader.GetVersionInfoAsync(versionId, cancellationToken);
    }

    /// <summary>
    /// 解析版本JSON为版本信息
    /// </summary>
    public LocalVersionInfo? ParseVersionJson(string json)
    {
        return VanillaGameDownloader.ParseVersionJson(json);
    }
}
