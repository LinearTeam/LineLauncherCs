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
using LMC.Basic.Logging;
using LMCCore.Game.Download.Model.Vanilla;
using LMCCore.Game.Model.LocalVersion;
using LMCCore.Game.Model.LocalVersion.Libraries;
using LMCCore.Utils;

namespace LMCCore.Game.Download.Vanilla;

public class VanillaGameDownloader(DownloadSourceManager? sourceManager = null)
{
    private readonly DownloadSourceManager _sourceManager = sourceManager ?? DownloadSourceManager.CreateDefault();
    private readonly static Logger s_logger = new("Download.Vanilla");
    private readonly static object s_manifestLock = new();
    private static Task<VersionManifestInfo>? s_manifestTask;

    public async Task<VersionManifestInfo> GetVersionManifestAsync(CancellationToken cancellationToken = default)
    {
        Task<VersionManifestInfo> manifestTask;
        lock (s_manifestLock)
        {
            s_manifestTask ??= FetchVersionManifestAsync();
            manifestTask = s_manifestTask;
        }

        try
        {
            return await manifestTask.WaitAsync(cancellationToken);
        }
        catch
        {
            lock (s_manifestLock)
            {
                if (ReferenceEquals(s_manifestTask, manifestTask) && manifestTask.IsFaulted)
                {
                    s_manifestTask = null;
                }
            }

            throw;
        }
    }

    async private Task<VersionManifestInfo> FetchVersionManifestAsync()
    {
        const string url = "https://launchermeta.mojang.com/mc/game/version_manifest.json";
        var transformedUrl = _sourceManager.TransformUrl(url);

        var response = await HttpUtils.CreateRequest(transformedUrl ?? url)
            .WithRetry(3)
            .WithRetryDelay(1000)
            .GetAsync();

        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadAsStringAsync();
        return JsonUtils.Parse(json).Get<VersionManifestInfo>() ??
               throw new InvalidOperationException("Failed to parse version manifest");
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
        return JsonUtils.Parse(json).Get<LocalVersionInfo>();
    }

    public static List<DownloadableFileInfo> GetLibrariesForDownload(LocalVersionInfo versionInfo)
    {
        var libraries = versionInfo.Libraries;
        var librariesShouldDownload = new List<DownloadableFileInfo>();

        if (libraries is not { Count: > 0 })
        {
            s_logger.Error("解析依赖库失败");
            return librariesShouldDownload;
        }

        s_logger.Info($"在版本 JSON 中找到 {libraries.Count} 个依赖库");

        foreach (var library in libraries)
        {
            if (string.IsNullOrWhiteSpace(library.Name))
            {
                s_logger.Warn("已跳过没有 name 的依赖库");
                continue;
            }

            switch (library)
            {
                case LibraryInfo detailedLibrary:
                    AppendDetailedLibraryDownloads(detailedLibrary, librariesShouldDownload);
                    break;
                case SimpleLibraryInfo simpleLibrary:
                    AppendSimpleLibraryDownload(simpleLibrary, librariesShouldDownload);
                    break;
                default:
                    s_logger.Warn($"已跳过未知格式的依赖库 {library.Name}");
                    break;
            }
        }

        s_logger.Info($"共解析 {librariesShouldDownload.Count} 个需要下载的依赖库");
        return librariesShouldDownload;
    }

    private static void AppendDetailedLibraryDownloads(LibraryInfo libInfo, ICollection<DownloadableFileInfo> librariesShouldDownload)
    {
        if (!CompatibilityRuleEvaluator.CheckRulesApply(libInfo.Rules))
        {
            s_logger.Info($"已跳过不适用的依赖库 {libInfo.Name}");
            return;
        }

        var hasNative = libInfo.Name.Contains("natives", StringComparison.OrdinalIgnoreCase) ||
                        libInfo.Natives is { Count: > 0 } ||
                        libInfo.Downloads?.Classifiers is { Count: > 0 };

        if (hasNative &&
            libInfo.Natives is { Count: > 0 } &&
            libInfo.Downloads?.Classifiers is { Count: > 0 })
        {
            var os = PlatformDetector.GetCurrentOs();
            if (libInfo.Natives.TryGetValue(os, out var key) &&
                libInfo.Downloads.Classifiers.TryGetValue(key, out var fileInfo))
            {
                if (string.IsNullOrWhiteSpace(fileInfo.Url))
                {
                    s_logger.Warn($"依赖库 {libInfo.Name} 的 {os} 本地库下载地址为空，已跳过");
                }
                else
                {
                    librariesShouldDownload.Add(fileInfo);
                }
            }
        }

        if (libInfo.Downloads?.Artifact?.Url is not null)
        {
            librariesShouldDownload.Add(libInfo.Downloads.Artifact);
        }
    }

    private static void AppendSimpleLibraryDownload(SimpleLibraryInfo libInfo, ICollection<DownloadableFileInfo> librariesShouldDownload)
    {
        if (string.IsNullOrWhiteSpace(libInfo.Url))
        {
            s_logger.Warn($"简单依赖库 {libInfo.Name} 缺少下载地址，已跳过");
            return;
        }

        if (!TryBuildMavenRelativePath(libInfo.Name, out var relativePath))
        {
            s_logger.Warn($"简单依赖库 {libInfo.Name} 的 Maven 坐标无法解析，已跳过");
            return;
        }

        if (string.IsNullOrWhiteSpace(libInfo.Sha1))
        {
            s_logger.Warn($"简单依赖库 {libInfo.Name} 缺少 sha1，将跳过校验");
        }

        var downloadUrl = new Uri(new Uri(EnsureTrailingSlash(libInfo.Url)), relativePath).ToString();
        librariesShouldDownload.Add(new DownloadableFileInfo
        {
            Path = relativePath,
            Url = downloadUrl,
            Sha1 = libInfo.Sha1,
            Size = libInfo.Size
        });
    }

    private static string EnsureTrailingSlash(string url) =>
        url.EndsWith("/", StringComparison.Ordinal) ? url : $"{url}/";

    private static bool TryBuildMavenRelativePath(string name, out string relativePath)
    {
        var parts = name.Split(':', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length is not (3 or 4))
        {
            relativePath = string.Empty;
            return false;
        }

        var group = parts[0].Replace('.', '/');
        var artifact = parts[1];
        var version = parts[2];
        var classifier = parts.Length == 4 ? parts[3] : null;
        var fileName = classifier == null
            ? $"{artifact}-{version}.jar"
            : $"{artifact}-{version}-{classifier}.jar";

        relativePath = $"{group}/{artifact}/{version}/{fileName}";
        return true;
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
                if (prop.Value is not JsonObject assetObj)
                {
                    continue;
                }

                var hashNode = assetObj["hash"];
                var sizeNode = assetObj["size"];

                if (hashNode == null || hashNode.GetValueKind() != JsonValueKind.String)
                {
                    continue;
                }

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

    public string GetLibraryDownloadUrl(string originalUrl)
    {
        return _sourceManager.TransformUrl(originalUrl) ?? originalUrl;
    }

    public string GetAssetDownloadUrl(string hash)
    {
        var originalUrl = $"https://resources.download.minecraft.net/{hash[..2]}/{hash}";
        return _sourceManager.TransformUrl(originalUrl) ?? originalUrl;
    }

    public static string GetAssetSavePath(string assetRoot, string hash)
    {
        return Path.Combine(assetRoot, "objects", hash[..2], hash);
    }

    public static string GetLibrarySavePath(string libraryRoot, string path)
    {
        return Path.Combine(libraryRoot, "libraries", path);
    }
}
