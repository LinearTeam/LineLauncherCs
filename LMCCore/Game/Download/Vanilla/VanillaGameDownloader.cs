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
    public const string OfficialLibraryBaseUrl = "https://libraries.minecraft.net/";

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

    private async Task<VersionManifestInfo> FetchVersionManifestAsync()
    {
        const string officialUrl = "https://launchermeta.mojang.com/mc/game/version_manifest.json";
        var transformedUrl = _sourceManager.TransformUrl(officialUrl) ?? officialUrl;
        string json;

        if (string.Equals(transformedUrl, officialUrl, StringComparison.OrdinalIgnoreCase))
        {
            json = await FetchVersionManifestJsonAsync(officialUrl);
        }
        else
        {
            try
            {
                json = await FetchVersionManifestJsonAsync(transformedUrl);
            }
            catch (Exception ex)
            {
                s_logger.Warn($"Failed to fetch version manifest from mirror '{transformedUrl}', falling back to official source. {ex.Message}");
                json = await FetchVersionManifestJsonAsync(officialUrl);
            }
        }

        return JsonUtils.Parse(json).Get<VersionManifestInfo>() ??
               throw new InvalidOperationException("Failed to parse version manifest");
    }

    internal static void ResetVersionManifestCacheForTesting()
    {
        lock (s_manifestLock)
        {
            s_manifestTask = null;
        }
    }

    public async Task<LocalVersionInfo?> GetVersionInfoAsync(string versionId, CancellationToken cancellationToken = default)
    {
        var json = await GetVersionJsonAsync(versionId, cancellationToken);
        return ParseVersionJson(json);
    }

    public async Task<LocalVersionInfo?> GetVersionInfoByUrlAsync(string versionJsonUrl, CancellationToken cancellationToken = default)
    {
        var json = await GetVersionJsonByUrlAsync(versionJsonUrl, cancellationToken);
        return ParseVersionJson(json);
    }

    public async Task<string> GetVersionJsonAsync(string versionId, CancellationToken cancellationToken = default)
    {
        var manifest = await GetVersionManifestAsync(cancellationToken);
        var versionEntry = manifest.Versions.FirstOrDefault(v => v.Id == versionId)
            ?? throw new ArgumentException($"Version {versionId} not found");

        return await GetVersionJsonByUrlAsync(versionEntry.Url, cancellationToken);
    }

    public async Task<string> GetVersionJsonByUrlAsync(string versionJsonUrl, CancellationToken cancellationToken = default)
    {
        var transformedUrl = _sourceManager.TransformUrl(versionJsonUrl);

        var response = await HttpUtils.CreateRequest(transformedUrl ?? versionJsonUrl)
            .WithRetry(3)
            .WithRetryDelay(1000)
            .GetAsync(cancellationToken);

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync(cancellationToken);
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
            s_logger.Error("Failed to parse libraries.");
            return librariesShouldDownload;
        }

        s_logger.Info($"Found {libraries.Count} libraries in version json.");

        foreach (var library in libraries)
        {
            if (string.IsNullOrWhiteSpace(library.Name))
            {
                s_logger.Warn("Skipped a library entry without a name.");
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
                    s_logger.Warn($"Skipped unsupported library entry format: {library.Name}");
                    break;
            }
        }

        s_logger.Info($"Resolved {librariesShouldDownload.Count} downloadable libraries.");
        return librariesShouldDownload;
    }

    private static void AppendDetailedLibraryDownloads(LibraryInfo libInfo, ICollection<DownloadableFileInfo> librariesShouldDownload)
    {
        if (!CompatibilityRuleEvaluator.CheckRulesApply(libInfo.Rules))
        {
            s_logger.Info($"Skipped incompatible library {libInfo.Name}");
            return;
        }

        var hasNative = libInfo.Natives is { Count: > 0 } ||
                        libInfo.Downloads?.Classifiers is { Count: > 0 };

        if (hasNative &&
            libInfo is { Natives.Count: > 0, Downloads.Classifiers.Count: > 0 })
        {
            var os = PlatformDetector.GetCurrentOs();
            if (libInfo.Natives.TryGetValue(os, out var key) &&
                libInfo.Downloads.Classifiers.TryGetValue(
                    key.Replace("${arch}", Environment.Is64BitOperatingSystem ? "64" : "32"),
                    out var fileInfo))
            {
                if (string.IsNullOrWhiteSpace(fileInfo.Url))
                {
                    s_logger.Warn($"Library {libInfo.Name} has no native download url for {os}.");
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
            return;
        }

        if (!string.IsNullOrWhiteSpace(libInfo.Url) &&
            TryBuildMavenRelativePath(libInfo.Name, out var relativePath))
        {
            var downloadUrl = new Uri(new Uri(EnsureTrailingSlash(libInfo.Url)), relativePath).ToString();
            librariesShouldDownload.Add(new DownloadableFileInfo
            {
                Path = relativePath,
                Url = downloadUrl,
                Sha1 = libInfo.GetPreferredSha1(),
                Checksums = libInfo.Checksums,
                Size = libInfo.Size
            });
        }
    }

    private static void AppendSimpleLibraryDownload(SimpleLibraryInfo libInfo, ICollection<DownloadableFileInfo> librariesShouldDownload)
    {
        if (!TryBuildMavenRelativePath(libInfo.Name, out var relativePath))
        {
            s_logger.Warn($"Simple library {libInfo.Name} does not have a valid Maven coordinate.");
            return;
        }

        if (string.IsNullOrWhiteSpace(libInfo.Sha1))
        {
            s_logger.Warn($"Simple library {libInfo.Name} is missing sha1, hash validation may be skipped.");
        }

        var hasExplicitUrl = !string.IsNullOrWhiteSpace(libInfo.Url);
        var baseUrl = hasExplicitUrl ? libInfo.Url! : OfficialLibraryBaseUrl;
        var downloadUrl = new Uri(new Uri(EnsureTrailingSlash(baseUrl)), relativePath).ToString();
        if (!hasExplicitUrl)
        {
            s_logger.Info($"Simple library {libInfo.Name} has no explicit download url, trying the official library source.");
        }

        librariesShouldDownload.Add(new DownloadableFileInfo
        {
            Path = relativePath,
            Url = downloadUrl,
            Sha1 = libInfo.GetPreferredSha1(),
            Checksums = libInfo.Checksums,
            Size = libInfo.Size,
            IgnoreNotFound = !hasExplicitUrl
        });
    }

    private static string EnsureTrailingSlash(string url) =>
        url.EndsWith("/", StringComparison.Ordinal) ? url : $"{url}/";

    private static async Task<string> FetchVersionManifestJsonAsync(string url)
    {
        using var response = await HttpUtils.CreateRequest(url)
            .WithRetry(3)
            .WithRetryDelay(1000)
            .GetAsync();

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync();
    }

    public static bool TryBuildMavenRelativePath(string name, out string relativePath)
    {
        var parts = name.Split(':', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 3)
        {
            relativePath = string.Empty;
            return false;
        }

        var normalizedParts = parts.ToArray();
        var extension = "jar";

        for (var i = 2; i < normalizedParts.Length; i++)
        {
            var atIndex = normalizedParts[i].LastIndexOf('@');
            if (atIndex < 0 || atIndex >= normalizedParts[i].Length - 1)
            {
                continue;
            }

            extension = normalizedParts[i][(atIndex + 1)..];
            normalizedParts[i] = normalizedParts[i][..atIndex];
            break;
        }

        var group = normalizedParts[0].Replace('.', '/');
        var artifact = normalizedParts[1];
        var version = normalizedParts[2];
        var classifier = normalizedParts.Length > 3
            ? string.Join('-', normalizedParts.Skip(3))
            : null;
        var fileName = string.IsNullOrWhiteSpace(classifier)
            ? $"{artifact}-{version}.{extension}"
            : $"{artifact}-{version}-{classifier}.{extension}";

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
