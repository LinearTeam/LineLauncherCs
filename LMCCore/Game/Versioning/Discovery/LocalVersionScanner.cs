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
using LMCCore.Game.Download;
using LMCCore.Game.Model;
using LMCCore.Game.Model.LocalVersion;

namespace LMCCore.Game.Versioning.Discovery;

internal sealed class LocalVersionScanner(
    DownloadManager downloadManager,
    VersionClientVersionResolver clientVersionResolver,
    Logger logger)
{
    private readonly DownloadManager _downloadManager = downloadManager;
    private readonly VersionClientVersionResolver _clientVersionResolver = clientVersionResolver;
    private readonly Logger _logger = logger;

    public async Task<IReadOnlyList<LocalGameVersionEntry>> ScanVersionsAsync(string rootPath, CancellationToken cancellationToken = default)
    {
        var normalizedRoot = ManagedGameRootService.EnsureExistingDirectory(rootPath);
        var versionsDirectory = Path.Combine(normalizedRoot, "versions");

        if (!Directory.Exists(versionsDirectory))
        {
            return [];
        }

        var versionDirectories = await Task.Run(() => Directory.GetDirectories(versionsDirectory), cancellationToken);
        var tasks = versionDirectories
            .Select(versionDirectory => CreateVersionEntryAsync(normalizedRoot, versionDirectory, cancellationToken))
            .ToArray();
        var entries = await Task.WhenAll(tasks);

        return entries
            .Where(entry => entry != null)
            .Cast<LocalGameVersionEntry>()
            .OrderBy(entry => entry.VersionName, StringComparer.OrdinalIgnoreCase)
            .ToList()
            .AsReadOnly();
    }

    public async Task<LocalGameVersionEntry?> CreateVersionEntryAsync(string rootPath, string versionDirectory, CancellationToken cancellationToken)
    {
        var versionName = Path.GetFileName(versionDirectory);
        var jarPath = Path.Combine(versionDirectory, $"{versionName}.jar");
        var jsonPath = Path.Combine(versionDirectory, $"{versionName}.json");
        var hasJar = File.Exists(jarPath);
        var hasJson = File.Exists(jsonPath);

        if (!hasJar && !hasJson)
        {
            return null;
        }

        LocalVersionInfo? versionInfo = null;
        var status = ResolveStatus(hasJar, hasJson);
        var clientVersionId = LocalGameVersionEntry.UnknownClientVersionId;

        if (!hasJson)
        {
            return CreateEntry(rootPath, versionName, versionDirectory, hasJar ? jarPath : null, null, clientVersionId, null, status);
        }

        try
        {
            var jsonContent = await File.ReadAllTextAsync(jsonPath, cancellationToken);
            versionInfo = _downloadManager.ParseVersionJson(jsonContent);
            if (versionInfo == null)
            {
                status = VersionStatus.InvalidJson;
            }
            else
            {
                clientVersionId = await _clientVersionResolver.ResolveAsync(versionInfo, cancellationToken);
                versionInfo.Type = GameVersionTypeClassifier.NormalizeLocalVersionType(versionInfo, versionName, clientVersionId);
            }
        }
        catch (Exception ex)
        {
            _logger.Warn($"读取或解析本地版本 JSON 失败 {jsonPath}: {ex.Message}");
            status = VersionStatus.InvalidJson;
        }

        return CreateEntry(rootPath, versionName, versionDirectory, hasJar ? jarPath : null, jsonPath, clientVersionId, versionInfo, status);
    }

    internal static VersionStatus ResolveStatus(bool hasJar, bool hasJson)
    {
        if (!hasJson)
        {
            return VersionStatus.MissingJson;
        }

        if (!hasJar)
        {
            return VersionStatus.MissingJar;
        }

        return VersionStatus.Valid;
    }

    private static LocalGameVersionEntry CreateEntry(
        string rootPath,
        string versionName,
        string versionDirectory,
        string? jarPath,
        string? jsonPath,
        string clientVersionId,
        LocalVersionInfo? versionInfo,
        VersionStatus status)
    {
        return new LocalGameVersionEntry
        {
            RootPath = rootPath,
            VersionName = versionName,
            VersionDirectory = versionDirectory,
            JarPath = jarPath,
            JsonPath = jsonPath,
            ClientVersionId = clientVersionId,
            VersionInfo = versionInfo,
            Status = status
        };
    }
}
