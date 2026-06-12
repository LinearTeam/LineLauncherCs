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
using LMCCore.Game.Download.Model;
using LMCCore.Game.Download.Installation.Caching;
using LMCCore.Game.Download.Vanilla;
using LMCCore.Game.Model.Loaders;
using LMCCore.Game.Model.LocalVersion;
using LMCCore.Tasks.Model;
using System.Text.Json;

namespace LMCCore.Game.Download.Installation;

public sealed class DownloadInstallationContext
{
    private readonly static JsonSerializerOptions s_serializerOptions = new()
    {
        WriteIndented = true
    };

    public required DownloadManager DownloadManager { get; init; }

    public required DownloadSourceManager DownloadSourceManager { get; init; }

    public required VanillaGameDownloader VanillaDownloader { get; init; }

    public required DownloadGamePlan Plan { get; init; }

    public required GameInstallationRuntimeState RuntimeState { get; init; }

    public required GameInstallationCacheManager CacheManager { get; init; }

    public required DownloadInstallationTaskRegistry Tasks { get; init; }

    public IReadOnlyList<IGameInstallationVersionJsonModifier> VersionJsonModifiers { get; set; } = [];

    public DownloadableGameVersion Request => Plan.Request;

    public ParentTask ParentTask => Plan.ParentTask;

    public bool HasLoader(ModLoaderType loaderType) =>
        Request.Loaders.Any(loader => loader.Type == loaderType);

    public ModLoader GetRequiredLoader(ModLoaderType loaderType)
    {
        return Request.Loaders.FirstOrDefault(loader => loader.Type == loaderType)
               ?? throw new InvalidOperationException($"Loader '{loaderType}' is not part of the current request.");
    }

    public LocalVersionInfo GetRequiredVersionInfo()
    {
        return RuntimeState.VersionInfo
               ?? throw new InvalidOperationException("Version info has not been resolved for the current installation.");
    }

    public string GetRequiredVersionJson()
    {
        return RuntimeState.VersionJson
               ?? throw new InvalidOperationException("Version json has not been resolved for the current installation.");
    }

    public async Task<string> ApplyVersionJsonModifiersAsync(string versionJson, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(versionJson);

        var currentVersionJson = versionJson;
        foreach (var modifier in VersionJsonModifiers)
        {
            currentVersionJson = await modifier.ModifyVersionJsonAsync(
                currentVersionJson,
                this,
                cancellationToken);
        }

        return currentVersionJson;
    }

    public async Task<LocalVersionInfo> PersistResolvedVersionJsonAsync(
        string versionJson,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(versionJson);

        var versionInfo = DownloadManager.ParseVersionJson(versionJson)
                          ?? throw new InvalidOperationException(
                              $"Failed to parse version metadata for {Request.VersionId}.");

        using var doc = JsonDocument.Parse(versionJson);
        var prettyJson = JsonSerializer.Serialize(doc.RootElement, s_serializerOptions);

        CacheManager.EnsureCacheDirectoryExists();
        await File.WriteAllTextAsync(
            CacheManager.CachedVersionJsonPath,
            prettyJson,
            cancellationToken);

        RuntimeState.VersionJson = prettyJson;
        RuntimeState.VersionInfo = versionInfo;
        return versionInfo;
    }
}
