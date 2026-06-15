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

using LMCCore.Game.Download.Installation;
using LMCCore.Game.Download.Installation.Caching;
using LMCCore.Game.Download.Installation.Providers;
using LMCCore.Game.Download.Model;
using LMCCore.Game.Download.Model.Vanilla;
using LMCCore.Game.Download.Planning;
using LMCCore.Game.Download.Vanilla;
using LMCCore.Game.Model.Loaders;
using LMCCore.Game.Model.LocalVersion;

namespace LMCCore.Game.Download;

public class DownloadManager
{
    private readonly DownloadSourceManager _downloadSourceManager;
    private readonly IReadOnlyList<IGameInstallationTaskProvider> _installationTaskProviders;
    private readonly Func<string, CancellationToken, Task<string>> _versionJsonResolver;
    private readonly DownloadGameTaskPlanner _taskPlanner = new();
    private VanillaGameDownloader? _vanillaDownloader;

    public DownloadManager() : this(DownloadSourceManager.CreateDefault())
    {
    }

    public DownloadManager(DownloadSourceManager sourceManager)
        : this(sourceManager, CreateDefaultInstallationTaskProviders(), null)
    {
    }

    internal DownloadManager(
        DownloadSourceManager sourceManager,
        IEnumerable<IGameInstallationTaskProvider> installationTaskProviders,
        Func<string, CancellationToken, Task<string>>? versionJsonResolver)
    {
        _downloadSourceManager = sourceManager ?? throw new ArgumentNullException(nameof(sourceManager));
        _installationTaskProviders = NormalizeInstallationTaskProviders(installationTaskProviders);
        _versionJsonResolver = versionJsonResolver ?? ((versionId, cancellationToken) =>
            VanillaDownloader.GetVersionJsonAsync(versionId, cancellationToken));
    }

    private VanillaGameDownloader VanillaDownloader =>
        _vanillaDownloader ??= new VanillaGameDownloader(_downloadSourceManager);

    public async Task<DownloadGamePlan> CreateDownloadPlanAsync(
        DownloadableGameVersion request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        EnsureRequestIsSupported(request);

        var plan = _taskPlanner.CreatePlan(request);
        var cacheManager = new GameInstallationCacheManager(plan.ParentTask);
        var runtimeState = new GameInstallationRuntimeState
        {
            CacheDirectory = cacheManager.CacheDirectory,
            CachedVersionJsonPath = cacheManager.CachedVersionJsonPath,
            CachedClientJarPath = cacheManager.CachedClientJarPath
        };
        var context = new DownloadInstallationContext
        {
            DownloadManager = this,
            DownloadSourceManager = _downloadSourceManager,
            VanillaDownloader = VanillaDownloader,
            Plan = plan,
            RuntimeState = runtimeState,
            CacheManager = cacheManager,
            Tasks = new DownloadInstallationTaskRegistry()
        };

        var applicableProviders = _installationTaskProviders
            .Where(provider => provider.ShouldApply(context))
            .OrderBy(GetProviderPlanningOrder)
            .ToList();
        context.VersionJsonModifiers = applicableProviders
            .OfType<IGameInstallationVersionJsonModifier>()
            .ToList()
            .AsReadOnly();

        foreach (var provider in applicableProviders)
        {
            provider.AddTasks(context);
        }

        context.CacheManager.RegisterCleanupOnTaskCompletion(context.Tasks.GetAllTasks());
        return plan;
    }

    /// <summary>
    /// Get the version manifest.
    /// </summary>
    public async Task<VersionManifestInfo> GetVersionManifestAsync(CancellationToken cancellationToken = default)
    {
        return await VanillaDownloader.GetVersionManifestAsync(cancellationToken);
    }

    /// <summary>
    /// Get the version info for the specified version id.
    /// </summary>
    public async Task<LocalVersionInfo?> GetVersionInfoAsync(string versionId, CancellationToken cancellationToken = default)
    {
        return await VanillaDownloader.GetVersionInfoAsync(versionId, cancellationToken);
    }

    public LocalVersionInfo? ParseVersionJson(string json)
    {
        return VanillaGameDownloader.ParseVersionJson(json);
    }

    internal Task<string> ResolveVersionJsonAsync(string versionId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(versionId);
        return _versionJsonResolver(versionId, cancellationToken);
    }

    private void EnsureRequestIsSupported(DownloadableGameVersion request)
    {
        foreach (var component in GetRequestedComponents(request))
        {
            if (_installationTaskProviders.Any(provider => provider.Component == component))
            {
                continue;
            }

            throw new NotSupportedException($"No installation task provider is registered for '{component}'.");
        }
    }

    private static IReadOnlyList<IGameInstallationTaskProvider> CreateDefaultInstallationTaskProviders()
    {
        return
        [
            new VanillaInstallationTaskProvider(),
            new FabricInstallationTaskProvider(),
            new ForgeInstallationTaskProvider(),
            new ForgePostProcessingTaskProvider(),
            new OptiFineInstallationTaskProvider(),
            new GameInstallationFinalizationTaskProvider()
        ];
    }

    private static IReadOnlyList<IGameInstallationTaskProvider> NormalizeInstallationTaskProviders(
        IEnumerable<IGameInstallationTaskProvider> installationTaskProviders)
    {
        ArgumentNullException.ThrowIfNull(installationTaskProviders);

        var providers = installationTaskProviders.ToList();
        if (providers.All(provider => provider.Component != DownloadInstallationComponent.Finalization))
        {
            providers.Add(new GameInstallationFinalizationTaskProvider());
        }

        return providers;
    }

    private static int GetProviderPlanningOrder(IGameInstallationTaskProvider provider)
    {
        return provider.Component switch
        {
            DownloadInstallationComponent.Finalization => 300,
            DownloadInstallationComponent.Vanilla => 100,
            _ when provider is IGameInstallationVersionJsonModifier => 0,
            _ => 200
        };
    }

    private static IEnumerable<DownloadInstallationComponent> GetRequestedComponents(DownloadableGameVersion request)
    {
        yield return DownloadInstallationComponent.Vanilla;

        foreach (var loader in request.Loaders)
        {
            yield return loader.Type switch
            {
                ModLoaderType.Fabric => DownloadInstallationComponent.Fabric,
                ModLoaderType.Forge => DownloadInstallationComponent.Forge,
                _ => throw new NotSupportedException($"Unsupported loader type '{loader.Type}'.")
            };
        }

        if (!string.IsNullOrWhiteSpace(request.OptiFine))
        {
            yield return DownloadInstallationComponent.OptiFine;
        }
    }
}
