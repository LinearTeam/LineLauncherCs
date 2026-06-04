using LMC;
using LMC.Basic.Configs;
using LMC.Basic.Logging;
using LMCCore.Game.Download;
using LMCCore.Game.Model;
using LMCCore.Game.Model.LocalVersion;
using LMCCore.Game.Versioning.Validation;
using LMC.Extensions.Hooks.Context;
using LMC.Extensions.Runtime;

namespace LMCCore.Game.Versioning;

public class VersionManager(DownloadManager? downloadManager = null, IEnumerable<IVersionValidator>? validators = null)
{
    private readonly IReadOnlyList<IVersionValidator> _validators = (validators ?? CreateDefaultValidators()).ToList().AsReadOnly();
    private readonly DownloadManager _downloadManager = downloadManager ?? new DownloadManager();
    private readonly Logger _logger = new("VersionManager");
    private LocalVersionScanner? _versionScanner;

    public IReadOnlyList<ManagedGameRoot> GetManagedRoots()
    {
        return CreateRootService().GetManagedRoots();
    }

    public void AddManagedRoot(string path)
    {
        var normalizedPath = ManagedGameRootService.EnsureExistingDirectory(path);
        var context = new ManagedRootExtensionContext
        {
            RootPath = normalizedPath
        };

        if (!LMCExtensionHost.Current.BeforeManagedRootAdd(context))
        {
            throw new InvalidOperationException("Messages.Extensions.ManagedRootAdd.Cancelled");
        }

        CreateRootService().AddManagedRoot(normalizedPath);
        LMCExtensionHost.Current.AfterManagedRootAdded(context);
    }

    public bool RemoveManagedRoot(string path)
    {
        return CreateRootService().RemoveManagedRoot(path);
    }

    public void SetSelectedRoot(string path)
    {
        var normalizedPath = ManagedGameRootService.EnsureExistingDirectory(path);
        CreateRootService().SetSelectedRoot(normalizedPath);
        LMCExtensionHost.Current.AfterSelectedRootChanged(new ManagedRootExtensionContext
        {
            RootPath = normalizedPath
        });
    }

    public ManagedGameRoot? GetSelectedRoot()
    {
        return CreateRootService().GetSelectedRoot();
    }

    public async Task<IReadOnlyList<LocalGameVersionEntry>> ScanVersionsAsync(string rootPath, CancellationToken cancellationToken = default)
    {
        var versions = await GetVersionScanner().ScanVersionsAsync(rootPath, cancellationToken);
        LMCExtensionHost.Current.AfterVersionsScanned(new VersionScanExtensionContext
        {
            RootPath = rootPath,
            VersionIds = versions.Select(version => version.VersionName).ToList().AsReadOnly()
        });
        return versions;
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

    private static IEnumerable<IVersionValidator> CreateDefaultValidators()
    {
        yield return new BasicVersionValidator();
    }

    private static void SaveConfig()
    {
        ConfigManager.Save("app", Current.Config);
    }
}
