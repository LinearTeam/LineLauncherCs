using LMC;
using LMC.Basic.Configs;
using LMCCore.Game.Model;

namespace LMCCore.Game.Versioning;

public class VersionManager
{
    private readonly IReadOnlyList<IVersionValidator> _validators;

    public VersionManager(IEnumerable<IVersionValidator>? validators = null)
    {
        _validators = (validators ?? CreateDefaultValidators()).ToList().AsReadOnly();
    }

    public IReadOnlyList<ManagedGameRoot> GetManagedRoots()
    {
        return Current.Config.ManagedGameRootPaths
            .Select(path => new ManagedGameRoot
            {
                RootPath = VersionPathUtils.NormalizePath(path)
            })
            .ToList()
            .AsReadOnly();
    }

    public void AddManagedRoot(string path)
    {
        var normalizedPath = EnsureExistingDirectory(path);
        if (Current.Config.ManagedGameRootPaths.Any(existing =>
                string.Equals(VersionPathUtils.NormalizePath(existing), normalizedPath, StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        Current.Config.ManagedGameRootPaths.Add(normalizedPath);
        if (string.IsNullOrWhiteSpace(Current.Config.SelectedGameRootPath))
        {
            Current.Config.SelectedGameRootPath = normalizedPath;
        }

        SaveConfig();
    }

    public bool RemoveManagedRoot(string path)
    {
        var normalizedPath = VersionPathUtils.NormalizePath(path);
        var removed = Current.Config.ManagedGameRootPaths.RemoveAll(existing =>
            string.Equals(VersionPathUtils.NormalizePath(existing), normalizedPath, StringComparison.OrdinalIgnoreCase)) > 0;

        if (!removed)
        {
            return false;
        }

        if (string.Equals(VersionPathUtils.NormalizePathOrEmpty(Current.Config.SelectedGameRootPath), normalizedPath, StringComparison.OrdinalIgnoreCase))
        {
            Current.Config.SelectedGameRootPath = Current.Config.ManagedGameRootPaths.FirstOrDefault() ?? string.Empty;
        }

        SaveConfig();
        return true;
    }

    public void SetSelectedRoot(string path)
    {
        var normalizedPath = EnsureExistingDirectory(path);
        if (!Current.Config.ManagedGameRootPaths.Any(existing =>
                string.Equals(VersionPathUtils.NormalizePath(existing), normalizedPath, StringComparison.OrdinalIgnoreCase)))
        {
            Current.Config.ManagedGameRootPaths.Add(normalizedPath);
        }

        Current.Config.SelectedGameRootPath = normalizedPath;
        SaveConfig();
    }

    public ManagedGameRoot? GetSelectedRoot()
    {
        if (string.IsNullOrWhiteSpace(Current.Config.SelectedGameRootPath))
        {
            return null;
        }

        return new ManagedGameRoot
        {
            RootPath = VersionPathUtils.NormalizePath(Current.Config.SelectedGameRootPath)
        };
    }

    public IReadOnlyList<LocalGameVersionEntry> ScanVersions(string rootPath)
    {
        var normalizedRoot = EnsureExistingDirectory(rootPath);
        var versionsDirectory = Path.Combine(normalizedRoot, "versions");

        if (!Directory.Exists(versionsDirectory))
        {
            return [];
        }

        return Directory.GetDirectories(versionsDirectory)
            .Select(versionDirectory => CreateVersionEntry(normalizedRoot, versionDirectory))
            .Where(entry => entry != null)
            .Cast<LocalGameVersionEntry>()
            .OrderBy(entry => entry.VersionName, StringComparer.OrdinalIgnoreCase)
            .ToList()
            .AsReadOnly();
    }

    public IReadOnlyList<LocalGameVersionEntry> ScanSelectedRootVersions()
    {
        var selectedRoot = GetSelectedRoot();
        return selectedRoot == null ? [] : ScanVersions(selectedRoot.RootPath);
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
        var versions = ScanVersions(rootPath);
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

    private static LocalGameVersionEntry? CreateVersionEntry(string rootPath, string versionDirectory)
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

        return new LocalGameVersionEntry
        {
            RootPath = rootPath,
            VersionName = versionName,
            VersionDirectory = versionDirectory,
            JarPath = hasJar ? jarPath : null,
            JsonPath = hasJson ? jsonPath : null,
            Status = ResolveStatus(hasJar, hasJson)
        };
    }

    private static VersionStatus ResolveStatus(bool hasJar, bool hasJson)
    {
        if (!hasJar)
        {
            return VersionStatus.MissingJar;
        }

        if (!hasJson)
        {
            return VersionStatus.MissingJson;
        }

        return VersionStatus.Valid;
    }

    private static string EnsureExistingDirectory(string path)
    {
        var normalizedPath = VersionPathUtils.NormalizePath(path);
        if (!Directory.Exists(normalizedPath))
        {
            throw new DirectoryNotFoundException($"游戏目录不存在: {normalizedPath}");
        }

        return normalizedPath;
    }

    private static IEnumerable<IVersionValidator> CreateDefaultValidators()
    {
        yield return new BasicVersionValidator();
    }

    private static void SaveConfig()
    {
        ConfigManager.Save("app", Current.Config);
    }
}
