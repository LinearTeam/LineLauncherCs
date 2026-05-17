using LMC;
using LMC.Basic.Configs;
using LMC.Basic.Logging;
using LMCCore.Game.Download;
using LMCCore.Game.Model;
using LMCCore.Game.Model.LocalVersion;
using LMCCore.Game.Model.LocalVersion.Arguments;

namespace LMCCore.Game.Versioning;

public class VersionManager(DownloadManager? downloadManager = null, IEnumerable<IVersionValidator>? validators = null)
{
    private readonly IReadOnlyList<IVersionValidator> _validators = (validators ?? CreateDefaultValidators()).ToList().AsReadOnly();
    private readonly DownloadManager _downloadManager = downloadManager ?? new DownloadManager();
    private readonly Logger _logger = new("VersionManager");

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

    public async Task<IReadOnlyList<LocalGameVersionEntry>> ScanVersionsAsync(string rootPath, CancellationToken cancellationToken = default)
    {
        var normalizedRoot = EnsureExistingDirectory(rootPath);
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

    public Task<IReadOnlyList<LocalGameVersionEntry>> ScanSelectedRootVersionsAsync(CancellationToken cancellationToken = default)
    {
        var selectedRoot = GetSelectedRoot();
        return selectedRoot == null
            ? Task.FromResult<IReadOnlyList<LocalGameVersionEntry>>([])
            : ScanVersionsAsync(selectedRoot.RootPath, cancellationToken);
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

    async private Task<LocalGameVersionEntry?> CreateVersionEntryAsync(string rootPath, string versionDirectory, CancellationToken cancellationToken)
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
            return new LocalGameVersionEntry
            {
                RootPath = rootPath,
                VersionName = versionName,
                VersionDirectory = versionDirectory,
                JarPath = hasJar ? jarPath : null,
                JsonPath = null,
                ClientVersionId = clientVersionId,
                VersionInfo = null,
                Status = status
            };
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
                clientVersionId = await ResolveClientVersionIdAsync(versionInfo, cancellationToken);
                versionInfo.Type = GameVersionTypeClassifier.NormalizeLocalVersionType(versionInfo, versionName, clientVersionId);
            }
        }
        catch (Exception ex)
        {
            _logger.Warn($"读取或解析本地版本 JSON 失败 {jsonPath}: {ex.Message}");
            status = VersionStatus.InvalidJson;
        }

        return new LocalGameVersionEntry
        {
            RootPath = rootPath,
            VersionName = versionName,
            VersionDirectory = versionDirectory,
            JarPath = hasJar ? jarPath : null,
            JsonPath = hasJson ? jsonPath : null,
            ClientVersionId = clientVersionId,
            VersionInfo = versionInfo,
            Status = status
        };
    }

    async private Task<string> ResolveClientVersionIdAsync(LocalVersionInfo versionInfo, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(versionInfo.ClientVersion))
        {
            return versionInfo.ClientVersion;
        }

        if (versionInfo.Patches?.Any(patch => patch.Id == "game") ?? false)
        {
            var patchVersion = versionInfo.Patches.First(patch => patch.Id == "game").Version;
            if (!string.IsNullOrEmpty(patchVersion))
            {
                return patchVersion;
            }
        }

        if (versionInfo.Arguments != null &&
            versionInfo.Arguments.TryGetValue("game", out var gameArgs) &&
            gameArgs is { Count: > 0 } &&
            gameArgs.Any(arg => arg is StringGameArgument sga && sga.Value.Trim().Equals("--fml.mcVersion", StringComparison.Ordinal)))
        {
            var index = gameArgs.FindIndex(arg => arg is StringGameArgument sga &&
                                                  sga.Value.Trim().Equals("--fml.mcVersion", StringComparison.Ordinal));
            if (index >= 0 && gameArgs.Count > index + 1 && gameArgs[index + 1] is StringGameArgument nextValue)
            {
                return nextValue.Value;
            }
        }

        if (TryResolveClientVersionIdFromLibraries(versionInfo, out var libraryVersion))
        {
            return libraryVersion;
        }

        if (versionInfo.ReleaseTime == null)
        {
            _logger.Warn($"版本 {versionInfo.Id} 无法识别");
            return LocalGameVersionEntry.UnknownClientVersionId;
        }

        try
        {
            var manifest = await _downloadManager.GetVersionManifestAsync(cancellationToken);
            var matchedVersion = manifest.Versions
                .Where(entry => entry.ReleaseTime == versionInfo.ReleaseTime.Value && entry.ReleaseTime.Year > 2009)
                .OrderByDescending(entry => string.Equals(entry.Type, "release", StringComparison.OrdinalIgnoreCase))
                .FirstOrDefault();

            if (matchedVersion == null)
            {
                _logger.Warn($"版本 {versionInfo.Id} 的 releaseTime ({versionInfo.ReleaseTime}) 未在版本清单中匹配到任何条目，无法识别客户端版本");
            }

            return matchedVersion?.Id ?? LocalGameVersionEntry.UnknownClientVersionId;
        }
        catch (Exception ex)
        {
            _logger.Warn($"根据 releaseTime 识别本地版本失败: {ex.Message}");
            return LocalGameVersionEntry.UnknownClientVersionId;
        }
    }

    private static bool TryResolveClientVersionIdFromLibraries(LocalVersionInfo versionInfo, out string clientVersionId)
    {
        clientVersionId = string.Empty;

        if (TryExtractVersionFromLibrary(versionInfo, "net.minecraftforge:fmlloader:", '-', out clientVersionId))
        {
            return true;
        }

        if (TryExtractVersionFromLibrary(versionInfo, "net.minecraftforge:forge:", '-', out clientVersionId))
        {
            return true;
        }

        if (TryExtractVersionFromLibrary(versionInfo, "optifine:OptiFine:", '_', out clientVersionId))
        {
            return true;
        }

        if (TryExtractVersionFromLibrary(versionInfo, "net.fabricmc:intermediary:", null, out clientVersionId))
        {
            return true;
        }

        return false;
    }

    private static bool TryExtractVersionFromLibrary(LocalVersionInfo versionInfo, string prefix, char? separator, out string clientVersionId)
    {
        clientVersionId = string.Empty;
        var library = versionInfo.Libraries.Find(item => item.Name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
        if (library == null)
        {
            return false;
        }

        var versionText = library.Name.Trim().Replace(prefix, string.Empty, StringComparison.OrdinalIgnoreCase);
        if (separator.HasValue)
        {
            var separatorIndex = versionText.IndexOf(separator.Value);
            if (separatorIndex > 0)
            {
                versionText = versionText[..separatorIndex];
            }
        }

        if (string.IsNullOrWhiteSpace(versionText))
        {
            return false;
        }

        clientVersionId = versionText;
        return true;
    }

    private static VersionStatus ResolveStatus(bool hasJar, bool hasJson)
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
