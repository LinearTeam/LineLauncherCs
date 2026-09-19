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

using System.Collections.Concurrent;
using LMC;
using LMC.Basic.Configs;
using LMC.Basic.Logging;
using LMCCore.Game.Download;
using LMCCore.Game.Download.Vanilla;
using LMCCore.Game.Launching;
using LMCCore.Game.Model;
using LMCCore.Game.Model.LocalVersion;
using LMCCore.Game.Model.Validation;
using LMCCore.Game.Versioning.Discovery;
using LMCCore.Game.Versioning.Validation;

namespace LMCCore.Game.Versioning;

public class VersionManager(
    DownloadManager? downloadManager = null,
    IEnumerable<IVersionValidator>? validators = null,
    GameLaunchManager? gameLaunchManager = null)
{
    private static readonly ConcurrentDictionary<string, IReadOnlyList<LocalGameVersionEntry>> s_versionScanCache =
        new(StringComparer.OrdinalIgnoreCase);
    private static readonly ConcurrentDictionary<string, Lazy<Task<IReadOnlyList<LocalGameVersionEntry>>>> s_versionScanTasks =
        new(StringComparer.OrdinalIgnoreCase);
    private static readonly ConcurrentDictionary<string, int> s_versionScanGenerations =
        new(StringComparer.OrdinalIgnoreCase);
    private static readonly ConcurrentDictionary<string, byte> s_unknownVersionRefreshes =
        new(StringComparer.OrdinalIgnoreCase);

    public static event Action<string, IReadOnlyList<LocalGameVersionEntry>>? UnknownVersionsRescanned;

    static VersionManager()
    {
        VanillaGameDownloader.VersionManifestLoaded += RefreshUnknownCachedVersions;
    }

    private readonly IReadOnlyList<IVersionValidator> _validators = (validators ?? CreateDefaultValidators()).ToList().AsReadOnly();
    private readonly DownloadManager _downloadManager = downloadManager ?? new DownloadManager();
    private readonly GameLaunchManager _gameLaunchManager = gameLaunchManager ?? new GameLaunchManager();
    private readonly Logger _logger = new("VersionManager");
    private LocalVersionScanner? _versionScanner;

    public IReadOnlyList<ManagedGameRoot> GetManagedRoots()
    {
        return CreateRootService().GetManagedRoots();
    }

    public void AddManagedRoot(string path)
    {
        MigrateLegacyLaunchVersionSelection(GetSelectedRoot());
        CreateRootService().AddManagedRoot(path);
        SynchronizeLegacyLaunchVersionSelection();
    }

    public bool RemoveManagedRoot(string path)
    {
        MigrateLegacyLaunchVersionSelection(GetSelectedRoot());
        var removed = CreateRootService().RemoveManagedRoot(path);
        if (removed)
        {
            SynchronizeLegacyLaunchVersionSelection();
        }

        return removed;
    }

    public void SetSelectedRoot(string path)
    {
        MigrateLegacyLaunchVersionSelection(GetSelectedRoot());
        CreateRootService().SetSelectedRoot(path);
        SynchronizeLegacyLaunchVersionSelection();
    }

    public ManagedGameRoot? GetSelectedRoot()
    {
        return CreateRootService().GetSelectedRoot();
    }

    public string GetSelectedLaunchVersionName(string rootPath)
    {
        var normalizedRootPath = NormalizeRootPath(rootPath);
        var selections = GetLaunchVersionSelections();
        if (selections.TryGetValue(normalizedRootPath, out var versionName))
        {
            return versionName;
        }

        var selectedRoot = GetSelectedRoot();
        if (selectedRoot == null ||
            !string.Equals(
                NormalizeRootPath(selectedRoot.RootPath),
                normalizedRootPath,
                StringComparison.OrdinalIgnoreCase) ||
            string.IsNullOrWhiteSpace(Current.Config.SelectedLaunchVersionName))
        {
            return string.Empty;
        }

        versionName = Current.Config.SelectedLaunchVersionName;
        selections[normalizedRootPath] = versionName;
        SaveConfig();
        return versionName;
    }

    public void SetSelectedLaunchVersionName(string rootPath, string versionName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(versionName);

        var normalizedRootPath = NormalizeRootPath(rootPath);
        GetLaunchVersionSelections()[normalizedRootPath] = versionName;

        var selectedRoot = GetSelectedRoot();
        if (selectedRoot != null &&
            string.Equals(
                NormalizeRootPath(selectedRoot.RootPath),
                normalizedRootPath,
                StringComparison.OrdinalIgnoreCase))
        {
            Current.Config.SelectedLaunchVersionName = versionName;
        }

        SaveConfig();
    }

    public Task<IReadOnlyList<LocalGameVersionEntry>> ScanVersionsAsync(
        string rootPath,
        CancellationToken cancellationToken = default)
    {
        return GetOrStartVersionScanAsync(rootPath).WaitAsync(cancellationToken);
    }

    public async Task<LocalGameVersionEntry> RenameVersionAsync(
        LocalGameVersionEntry version,
        string newName,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(version);
        LocalVersionRenamer.ValidateVersionName(newName);
        cancellationToken.ThrowIfCancellationRequested();

        if (string.Equals(version.VersionName, newName, StringComparison.Ordinal))
        {
            return version;
        }

        await Task.Run(() => new LocalVersionRenamer().Rename(version, newName), cancellationToken);
        UpdateSelectedVersionAfterRename(version.RootPath, version.VersionName, newName);

        var versions = await RefreshVersionsAsync(version.RootPath, cancellationToken);
        return versions.FirstOrDefault(item =>
                   string.Equals(item.VersionName, newName, StringComparison.Ordinal))
               ?? throw new InvalidOperationException($"Renamed version '{newName}' could not be loaded.");
    }

    public async Task<IReadOnlyList<LocalGameVersionEntry>> RefreshVersionsAsync(
        string rootPath,
        CancellationToken cancellationToken = default)
    {
        var normalizedRootPath = NormalizeRootPath(rootPath);
        var generation = s_versionScanGenerations.AddOrUpdate(normalizedRootPath, 1, (_, current) => current + 1);
        s_versionScanCache.TryRemove(normalizedRootPath, out _);
        s_versionScanTasks.TryRemove(normalizedRootPath, out _);

        var versions = await GetVersionScanner().ScanVersionsAsync(rootPath, cancellationToken);
        var snapshot = versions.ToList().AsReadOnly();
        if (s_versionScanGenerations.TryGetValue(normalizedRootPath, out var currentGeneration) &&
            currentGeneration == generation)
        {
            s_versionScanCache[normalizedRootPath] = snapshot;
        }

        return snapshot;
    }

    public Task<IReadOnlyList<LocalGameVersionEntry>> ScanSelectedRootVersionsAsync(CancellationToken cancellationToken = default)
    {
        var selectedRoot = GetSelectedRoot();
        return selectedRoot == null
            ? Task.FromResult<IReadOnlyList<LocalGameVersionEntry>>([])
            : ScanVersionsAsync(selectedRoot.RootPath, cancellationToken);
    }

    public Task<IReadOnlyList<LocalGameVersionEntry>> GetCachedOrScanSelectedRootVersionsAsync(
        CancellationToken cancellationToken = default)
    {
        var selectedRoot = GetSelectedRoot();
        return selectedRoot == null
            ? Task.FromResult<IReadOnlyList<LocalGameVersionEntry>>([])
            : GetCachedOrScanVersionsAsync(selectedRoot.RootPath, cancellationToken);
    }

    public Task<IReadOnlyList<LocalGameVersionEntry>> GetCachedOrScanVersionsAsync(
        string rootPath,
        CancellationToken cancellationToken = default)
    {
        var normalizedRootPath = NormalizeRootPath(rootPath);
        return s_versionScanCache.TryGetValue(normalizedRootPath, out var versions)
            ? Task.FromResult(versions)
            : GetOrStartVersionScanAsync(rootPath).WaitAsync(cancellationToken);
    }

    public bool TryGetCachedSelectedRootVersions(out IReadOnlyList<LocalGameVersionEntry> versions)
    {
        var selectedRoot = GetSelectedRoot();
        if (selectedRoot == null)
        {
            versions = [];
            return false;
        }

        return s_versionScanCache.TryGetValue(NormalizeRootPath(selectedRoot.RootPath), out versions!);
    }

    /// <summary>
    /// Resolves the version to show on the launch page without waiting for a full catalog scan.
    /// A complete scan may need to download the version manifest merely to improve display metadata,
    /// whereas this method only reads local files needed to validate the selected version.
    /// </summary>
    public async Task<LocalGameVersionEntry?> ResolveSelectedLaunchVersionAsync(
        CancellationToken cancellationToken = default)
    {
        var selectedRoot = GetSelectedRoot();
        if (selectedRoot == null)
        {
            return null;
        }

        var rootPath = selectedRoot.RootPath;
        var selectedVersionName = GetSelectedLaunchVersionName(rootPath);
        if (s_versionScanCache.TryGetValue(NormalizeRootPath(rootPath), out var cachedVersions))
        {
            return SelectUsableVersion(rootPath, selectedVersionName, cachedVersions);
        }

        if (!Directory.Exists(rootPath))
        {
            return null;
        }

        var selectedVersion = await TryReadLaunchableVersionAsync(rootPath, selectedVersionName, cancellationToken);
        if (selectedVersion != null)
        {
            return selectedVersion;
        }

        var fallbackVersion = await FindFirstLaunchableVersionAsync(rootPath, cancellationToken);
        if (fallbackVersion != null &&
            !string.Equals(fallbackVersion.VersionName, selectedVersionName, StringComparison.Ordinal))
        {
            SetSelectedLaunchVersionName(rootPath, fallbackVersion.VersionName);
        }

        return fallbackVersion;
    }

    public async Task WarmSelectedRootVersionsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var selectedRoot = GetSelectedRoot();
            if (selectedRoot == null)
            {
                return;
            }

            await GetCachedOrScanVersionsAsync(selectedRoot.RootPath, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            _logger.Debug($"Prewarming local version scan cache failed: {ex}");
        }
    }

    public void StartWarmSelectedRootVersions()
    {
        _ = Task.Run(async () => await WarmSelectedRootVersionsAsync());
    }

    public async Task<VersionValidationResult> ValidateVersionAsync(LocalGameVersionEntry version, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var allIssues = new List<VersionValidationIssue>();
        foreach (var validator in _validators)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var result = await validator.ValidateAsync(version, cancellationToken);
            allIssues.AddRange(result.Issues);
        }

        return new VersionValidationResult
        {
            IsValid = allIssues.All(issue => issue.Severity != VersionValidationSeverity.Error),
            Issues = allIssues
        };
    }

    public async Task<IReadOnlyDictionary<LocalGameVersionEntry, VersionValidationResult>> ValidateVersionsAsync(string rootPath, CancellationToken cancellationToken = default)
    {
        var versions = await ScanVersionsAsync(rootPath, cancellationToken);
        var results = new Dictionary<LocalGameVersionEntry, VersionValidationResult>();

        foreach (var version in versions)
        {
            cancellationToken.ThrowIfCancellationRequested();
            results[version] = await ValidateVersionAsync(version, cancellationToken);
        }

        return results;
    }

    public Task<IReadOnlyDictionary<LocalGameVersionEntry, VersionValidationResult>> ValidateSelectedRootVersionsAsync(CancellationToken cancellationToken = default)
    {
        var selectedRoot = GetSelectedRoot();
        if (selectedRoot == null)
        {
            return Task.FromResult<IReadOnlyDictionary<LocalGameVersionEntry, VersionValidationResult>>(
                new Dictionary<LocalGameVersionEntry, VersionValidationResult>());
        }

        return ValidateVersionsAsync(selectedRoot.RootPath, cancellationToken);
    }

    public Task<GameLaunchResult> LaunchGameAsync(
        LocalGameVersionEntry version,
        AppConfig config,
        LMCCore.Account.Model.Account account,
        bool shouldValidateAndCompleteMissingFiles = true,
        IProgress<GameLaunchProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(version);
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(account);

        return _gameLaunchManager.LaunchAsync(
            version,
            config,
            account,
            shouldValidateAndCompleteMissingFiles,
            progress,
            cancellationToken);
    }

    public void TerminateGame(GameLaunchResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        _gameLaunchManager.Terminate(result);
    }

    private ManagedGameRootService CreateRootService()
    {
        return new ManagedGameRootService(Current.Config, SaveConfig);
    }

    private LocalVersionScanner GetVersionScanner()
    {
        return _versionScanner ??= new LocalVersionScanner(
            _downloadManager,
            new VersionClientVersionResolver(
                cancellationToken => _downloadManager.GetVersionManifestAsync(cancellationToken),
                _logger),
            _logger);
    }

    private Task<IReadOnlyList<LocalGameVersionEntry>> GetOrStartVersionScanAsync(string rootPath)
    {
        var normalizedRootPath = NormalizeRootPath(rootPath);
        var generation = s_versionScanGenerations.GetOrAdd(normalizedRootPath, 0);
        Lazy<Task<IReadOnlyList<LocalGameVersionEntry>>>? newScan = null;
        newScan = new Lazy<Task<IReadOnlyList<LocalGameVersionEntry>>>(async () =>
        {
            var manifestGeneration = VanillaGameDownloader.ManifestGeneration;
            try
            {
                var versions = await GetVersionScanner().ScanVersionsAsync(rootPath);
                var snapshot = versions.ToList().AsReadOnly();
                if (s_versionScanGenerations.TryGetValue(normalizedRootPath, out var currentGeneration) &&
                    currentGeneration == generation)
                {
                    s_versionScanCache[normalizedRootPath] = snapshot;
                }
                if (manifestGeneration != VanillaGameDownloader.ManifestGeneration &&
                    snapshot.Any(NeedsManifestResolution))
                {
                    StartUnknownVersionRefresh(normalizedRootPath);
                }

                return snapshot;
            }
            finally
            {
                if (s_versionScanTasks.TryGetValue(normalizedRootPath, out var currentScan) &&
                    ReferenceEquals(currentScan, newScan))
                {
                    s_versionScanTasks.TryRemove(normalizedRootPath, out _);
                }
            }
        }, LazyThreadSafetyMode.ExecutionAndPublication);

        return s_versionScanTasks.GetOrAdd(normalizedRootPath, newScan).Value;
    }

    private static void RefreshUnknownCachedVersions()
    {
        foreach (var (rootPath, versions) in s_versionScanCache)
        {
            if (versions.Any(NeedsManifestResolution))
            {
                StartUnknownVersionRefresh(rootPath);
            }
        }
    }

    private static bool NeedsManifestResolution(LocalGameVersionEntry version)
    {
        return version.ClientVersionId == LocalGameVersionEntry.UnknownClientVersionId &&
               version.VersionInfo?.ReleaseTime != null;
    }

    private static void StartUnknownVersionRefresh(string rootPath)
    {
        if (!s_unknownVersionRefreshes.TryAdd(rootPath, 0))
        {
            return;
        }

        _ = Task.Run(async () =>
        {
            try
            {
                await RefreshUnknownVersionsAsync(rootPath);
            }
            catch (Exception ex)
            {
                new Logger("VersionManager").Warn($"Refreshing unknown versions in '{rootPath}' failed: {ex}");
            }
            finally
            {
                s_unknownVersionRefreshes.TryRemove(rootPath, out _);
            }
        });
    }

    private static async Task RefreshUnknownVersionsAsync(string rootPath)
    {
        var scanner = new VersionManager().GetVersionScanner();
        for (var attempt = 0; attempt < 3; attempt++)
        {
            if (!s_versionScanCache.TryGetValue(rootPath, out var cachedVersions) ||
                !cachedVersions.Any(NeedsManifestResolution))
            {
                return;
            }

            var refreshedVersions = await Task.WhenAll(cachedVersions.Select(async version =>
                NeedsManifestResolution(version)
                    ? await scanner.CreateVersionEntryAsync(
                        rootPath,
                        version.VersionDirectory,
                        CancellationToken.None)
                    : version));
            var snapshot = refreshedVersions
                .Where(version => version != null)
                .Cast<LocalGameVersionEntry>()
                .ToList()
                .AsReadOnly();

            if (s_versionScanCache.TryUpdate(rootPath, snapshot, cachedVersions))
            {
                UnknownVersionsRescanned?.Invoke(rootPath, snapshot);
                return;
            }
        }
    }

    private LocalGameVersionEntry? SelectUsableVersion(
        string rootPath,
        string selectedVersionName,
        IReadOnlyList<LocalGameVersionEntry> versions)
    {
        var selectedVersion = versions.FirstOrDefault(version =>
            string.Equals(version.VersionName, selectedVersionName, StringComparison.Ordinal) &&
            IsLaunchable(version));
        if (selectedVersion != null)
        {
            return selectedVersion;
        }

        var fallbackVersion = versions.FirstOrDefault(IsLaunchable);
        if (fallbackVersion != null &&
            !string.Equals(fallbackVersion.VersionName, selectedVersionName, StringComparison.Ordinal))
        {
            SetSelectedLaunchVersionName(rootPath, fallbackVersion.VersionName);
        }

        return fallbackVersion;
    }

    private async Task<LocalGameVersionEntry?> FindFirstLaunchableVersionAsync(
        string rootPath,
        CancellationToken cancellationToken)
    {
        var versionsDirectory = Path.Combine(rootPath, "versions");
        if (!Directory.Exists(versionsDirectory))
        {
            return null;
        }

        string[] versionDirectories;
        try
        {
            versionDirectories = await Task.Run(
                () => Directory.GetDirectories(versionsDirectory)
                    .OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase)
                    .ToArray(),
                cancellationToken);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _logger.Warn($"Unable to enumerate local versions in '{versionsDirectory}': {ex.Message}");
            return null;
        }

        foreach (var versionDirectory in versionDirectories)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var version = await GetVersionScanner().CreateVersionEntryAsync(
                rootPath,
                versionDirectory,
                cancellationToken,
                resolveClientVersion: false);
            if (IsLaunchable(version))
            {
                return version;
            }
        }

        return null;
    }

    private async Task<LocalGameVersionEntry?> TryReadLaunchableVersionAsync(
        string rootPath,
        string versionName,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(versionName) ||
            versionName is "." or ".." ||
            versionName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 ||
            versionName.Contains(Path.DirectorySeparatorChar) ||
            versionName.Contains(Path.AltDirectorySeparatorChar))
        {
            return null;
        }

        var versionDirectory = Path.Combine(rootPath, "versions", versionName);
        if (!Directory.Exists(versionDirectory))
        {
            return null;
        }

        var version = await GetVersionScanner().CreateVersionEntryAsync(
            rootPath,
            versionDirectory,
            cancellationToken,
            resolveClientVersion: false);
        return IsLaunchable(version) ? version : null;
    }

    private static bool IsLaunchable(LocalGameVersionEntry? version)
    {
        return version is { Status: VersionStatus.Valid, VersionInfo: not null };
    }

    private static string NormalizeRootPath(string rootPath)
    {
        var fullPath = Path.GetFullPath(rootPath);
        var root = Path.GetPathRoot(fullPath) ?? string.Empty;
        return fullPath.Length > root.Length
            ? fullPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            : fullPath;
    }

    private static IEnumerable<IVersionValidator> CreateDefaultValidators()
    {
        yield return new BasicVersionValidator();
    }

    private static void SaveConfig()
    {
        ConfigManager.Save("app", Current.Config);
    }

    private static Dictionary<string, string> GetLaunchVersionSelections()
    {
        return Current.Config.SelectedLaunchVersionsByGameRoot ??= [];
    }

    private static void UpdateSelectedVersionAfterRename(string rootPath, string oldName, string newName)
    {
        var normalizedRootPath = NormalizeRootPath(rootPath);
        var selections = GetLaunchVersionSelections();
        if (selections.TryGetValue(normalizedRootPath, out var selectedVersion) &&
            string.Equals(selectedVersion, oldName, StringComparison.Ordinal))
        {
            selections[normalizedRootPath] = newName;
        }

        var selectedRoot = new ManagedGameRootService(Current.Config, SaveConfig).GetSelectedRoot();
        if (selectedRoot != null &&
            string.Equals(NormalizeRootPath(selectedRoot.RootPath), normalizedRootPath,
                StringComparison.OrdinalIgnoreCase) &&
            string.Equals(Current.Config.SelectedLaunchVersionName, oldName, StringComparison.Ordinal))
        {
            Current.Config.SelectedLaunchVersionName = newName;
        }

        SaveConfig();
    }

    private static void MigrateLegacyLaunchVersionSelection(ManagedGameRoot? root)
    {
        if (root == null || string.IsNullOrWhiteSpace(Current.Config.SelectedLaunchVersionName))
        {
            return;
        }

        GetLaunchVersionSelections().TryAdd(
            NormalizeRootPath(root.RootPath),
            Current.Config.SelectedLaunchVersionName);
    }

    private void SynchronizeLegacyLaunchVersionSelection()
    {
        var selectedRoot = GetSelectedRoot();
        if (selectedRoot == null)
        {
            Current.Config.SelectedLaunchVersionName = string.Empty;
            SaveConfig();
            return;
        }

        Current.Config.SelectedLaunchVersionName = GetLaunchVersionSelections()
            .GetValueOrDefault(NormalizeRootPath(selectedRoot.RootPath), string.Empty);
        SaveConfig();
    }
}
