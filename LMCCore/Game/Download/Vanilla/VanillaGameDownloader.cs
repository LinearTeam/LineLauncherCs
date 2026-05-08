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

using System.Text.Json;
using System.Text.Json.Nodes;
using LMCCore.Game.Download.Model.Vanilla;
using LMCCore.Utils;
using LMC.Basic.Logging;
using LMCCore.Game.Model.LocalVersion;

namespace LMCCore.Game.Download.Vanilla;

/// <summary>
/// 原版游戏下载器核心类
/// </summary>
public class VanillaGameDownloader(DownloadSourceManager? sourceManager = null)
{
    private readonly DownloadSourceManager _sourceManager = sourceManager ?? DownloadSourceManager.CreateDefault();
    private readonly static Logger s_logger = new("Download.Vanilla");

    public async Task<VersionManifestInfo> GetVersionManifestAsync(CancellationToken cancellationToken = default)
    {
        var url = "https://launchermeta.mojang.com/mc/game/version_manifest.json";
        var transformedUrl = _sourceManager.TransformUrl(url);

        var response = await HttpUtils.CreateRequest(transformedUrl ?? url)
            .WithRetry(3)
            .WithRetryDelay(1000)
            .GetAsync(cancellationToken);

        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        return JsonUtils.Parse(json).Get<VersionManifestInfo>() ?? throw new InvalidOperationException("Failed to parse version manifest");
    }

    public async Task<LocalVersionInfo?> GetVersionInfoAsync(string versionId, CancellationToken cancellationToken = default)
    {
        var manifest = await GetVersionManifestAsync(cancellationToken);
        var versionEntry = manifest.Versions.FirstOrDefault(v => v.Id == versionId)
            ?? throw new ArgumentException($"Version {versionId} not found");

        return await GetVersionInfoByUrlAsync(versionEntry.Url, cancellationToken);
    }

    public async Task<LocalVersionInfo?> GetVersionInfoByUrlAsync(string versionJsonUrl, CancellationToken cancellationToken = default)
    {
        var transformedUrl = _sourceManager.TransformUrl(versionJsonUrl);

        var response = await HttpUtils.CreateRequest(transformedUrl ?? versionJsonUrl)
            .WithRetry(3)
            .WithRetryDelay(1000)
            .GetAsync(cancellationToken);

        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        return ParseVersionJson(json);
    }

    public static LocalVersionInfo? ParseVersionJson(string json)
    {
        return JsonUtils.Parse(json)
            .Get<LocalVersionInfo>();
    }

    public static List<DownloadableFileInfo> GetLibrariesForDownload(LocalVersionInfo versionInfo)
    {
        var libraries = versionInfo.Libraries;
        var librariesShouldDownload = new List<DownloadableFileInfo>();

        if (libraries is { Count: > 0 })
        {
            s_logger.Info($"在版本JSON中找到 {libraries.Count} 个依赖库");

            foreach (var libInfo in libraries)
            {
                var name = libInfo.Name;
                if (string.IsNullOrEmpty(name))
                {
                    s_logger.Warn($"已跳过没有name的依赖库");
                    continue;
                }

                var rules = libInfo.Rules;
                if (!CompatibilityRuleEvaluator.CheckRulesApply(rules))
                {
                    s_logger.Info($"已跳过不适用的依赖库{name}");
                    continue;
                }

                var hasNative = libInfo.Name.Contains("natives") ||
                                libInfo.Natives is { Count: > 0 } ||
                                libInfo.Downloads?.Classifiers is { Count: > 0 };

                if (hasNative)
                {
                    if (libInfo is { Natives.Count: > 0, Downloads.Classifiers.Count: > 0 })
                    {
                        var os = PlatformDetector.GetCurrentOs();
                        if (libInfo.Natives.TryGetValue(os, out var key))
                        {
                            var fileInfo = libInfo.Downloads.Classifiers[key];
                            if (fileInfo.Url == null)
                            {
                                s_logger.Warn($"依赖库 {name} 的 {os} 本地库下载地址为空，跳过");
                            }
                            else
                            {
                                librariesShouldDownload.Add(fileInfo);
                            }
                        }
                    }
                }

                if (libInfo.Downloads?.Artifact?.Url is not null)
                {
                    librariesShouldDownload.Add(libInfo.Downloads.Artifact);
                }
            }

            s_logger.Info($"共解析 {librariesShouldDownload.Count} 个需要下载的依赖库");
        }
        else
        {
            s_logger.Error("解析依赖库失败");
        }
        return librariesShouldDownload;
    }

    public async Task<Dictionary<string, AssetInfo>> GetAssetIndexAsync(AssetIndexInfo assetIndex, CancellationToken cancellationToken = default)
    {
        var transformedUrl = _sourceManager.TransformUrl(assetIndex.Url!);

        var response = await HttpUtils.CreateRequest((transformedUrl ?? assetIndex.Url)!)
            .WithRetry(3)
            .WithRetryDelay(1000)
            .GetAsync(cancellationToken);

        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadAsStringAsync(cancellationToken);

        return ParseAssetIndex(json);
    }
    
    
    public async Task<string> GetAssetIndexJsonAsync(AssetIndexInfo? assetIndex, CancellationToken cancellationToken = default)
    {
        if (assetIndex?.Url == null)
        {
            throw new NullReferenceException("AssetIndex or it's url is null");
        }
        var transformedUrl = _sourceManager.TransformUrl(assetIndex.Url);

        var response = await HttpUtils.CreateRequest(transformedUrl ?? assetIndex.Url)
            .WithRetry(3)
            .WithRetryDelay(1000)
            .GetAsync(cancellationToken);

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync(cancellationToken);
    }
    
    
    public Dictionary<string, AssetInfo> ParseAssetIndex(string json)
    {
        var assets = new Dictionary<string, AssetInfo>();
        var objectsNode = JsonUtils.Parse(json).GetObject("objects");

        if (objectsNode is { IsValid: true, Node: JsonObject objects })
        {
            foreach (var prop in objects)
            {
                if (prop.Value is not JsonObject assetObj) continue;

                var hashNode = assetObj["hash"];
                var sizeNode = assetObj["size"];
                
                if (hashNode == null || hashNode.GetValueKind() != JsonValueKind.String) continue;

                var hash = hashNode.GetValue<string>();
                long size = sizeNode?.GetValueKind() == JsonValueKind.Number 
                    ? sizeNode.GetValue<long>() 
                    : -1;

                assets[prop.Key] = new AssetInfo
                {
                    Hash = hash,
                    Size = size
                };
            }
        }

        return assets;
    }

    /// <summary>
    /// 获取依赖库下载链接（已转换）
    /// </summary>
    public string GetLibraryDownloadUrl(string originalUrl)
    {
        return _sourceManager.TransformUrl(originalUrl) ?? originalUrl;
    }

    /// <summary>
    /// 获取资源文件下载链接（已转换）
    /// </summary>
    public string GetAssetDownloadUrl(string hash)
    {
        var originalUrl = $"https://resources.download.minecraft.net/{hash[..2]}/{hash}";
        return _sourceManager.TransformUrl(originalUrl) ?? originalUrl;
    }

    /// <summary>
    /// 获取资源文件保存路径
    /// </summary>
    public static string GetAssetSavePath(string assetRoot, string hash)
    {
        return Path.Combine(assetRoot, "objects", hash[..2], hash);
    }

    /// <summary>
    /// 获取依赖库保存路径
    /// </summary>
    public static string GetLibrarySavePath(string libraryRoot, string path)
    {
        return Path.Combine(libraryRoot, "libraries", path);
    }
}
