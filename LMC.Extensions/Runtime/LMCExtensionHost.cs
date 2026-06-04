using LMC.Extensions.Abstractions;
using LMC.Extensions.Hooks;
using LMC.Extensions.Hooks.Context;
using LMC.Extensions.Loading;
using LMC.Extensions.Packaging;
using LMC.Extensions.Resolution;
using LMC.Extensions.UI;

namespace LMC.Extensions.Runtime;

public sealed class LMCExtensionHost : IDisposable
{
    private sealed class NullUIExtensionApi : IUIExtensionApi
    {
        public void RegisterPage(UIExtensionPageRegistration registration) { }

        public void RegisterNavigationItem(UIExtensionNavigationItem navigationItem) { }
    }

    private readonly Dictionary<string, LoadedLMCExtension> _loadedExtensions = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<IStartupExtensionHooks> _startupHooks = [];
    private readonly List<IAccountExtensionHooks> _accountHooks = [];
    private readonly List<IVersioningExtensionHooks> _versioningHooks = [];
    private readonly List<ITaskExtensionHooks> _taskHooks = [];
    private readonly HashSet<string> _disabledExtensionIds;
    private readonly IExtensionPackageReader _packageReader;
    private readonly IExtensionResolver _resolver;
    private readonly ILMCExtensionLoader _loader;
    private readonly ILMCExtensionLoggerFactory _loggerFactory;
    private readonly IUIExtensionApi _uiApi;
    private readonly ILMCExtensionLogger _logger;
    private bool _disposed;

    public LMCExtensionHost(
        LMCExtensionHostOptions options,
        IExtensionPackageReader? packageReader = null,
        IExtensionResolver? resolver = null,
        ILMCExtensionLoader? loader = null)
    {
        Options = options;
        HostVersion = options.HostVersion;
        ExtensionsDirectory = options.ExtensionsDirectory;
        _disabledExtensionIds = options.DisabledExtensionIds.ToHashSet(StringComparer.OrdinalIgnoreCase);
        _loggerFactory = options.LoggerFactory ?? NullLMCExtensionLoggerFactory.Instance;
        _packageReader = packageReader ?? new ExtensionPackageReader(_loggerFactory.CreateLogger(nameof(ExtensionPackageReader)));
        _resolver = resolver ?? new ExtensionResolver(_loggerFactory.CreateLogger(nameof(ExtensionResolver)));
        _loader = loader ?? new LMCExtensionLoader(
            new LMCExtensionPackageExtractor(_loggerFactory.CreateLogger(nameof(LMCExtensionPackageExtractor))),
            _loggerFactory.CreateLogger(nameof(LMCExtensionLoader)));
        _uiApi = options.UIApi ?? new NullUIExtensionApi();
        _logger = _loggerFactory.CreateLogger(nameof(LMCExtensionHost));
    }

    public static LMCExtensionHost Current { get; private set; } = CreateEmptyHost();

    public LMCExtensionHostOptions Options { get; }

    public string HostVersion { get; }

    public string ExtensionsDirectory { get; }

    public IReadOnlyList<ExtensionLoadResult> LoadResults { get; private set; } = [];

    public static LMCExtensionHost InitializeCurrent(
        string hostVersion,
        string extensionsDirectory,
        IEnumerable<string>? disabledExtensionIds = null,
        ILMCExtensionLoggerFactory? loggerFactory = null,
        IUIExtensionApi? uiApi = null)
    {
        Current.Dispose();
        var host = new LMCExtensionHost(new LMCExtensionHostOptions
        {
            HostVersion = hostVersion,
            ExtensionsDirectory = extensionsDirectory,
            DisabledExtensionIds = disabledExtensionIds?.ToList().AsReadOnly() ?? [],
            LoggerFactory = loggerFactory,
            UIApi = uiApi
        });
        host.LoadExtensions();
        Current = host;
        return host;
    }

    public static void ResetCurrent()
    {
        Current.Dispose();
        Current = CreateEmptyHost();
    }

    public void NotifyHostInitializing()
    {
        ThrowIfDisposed();
        _logger.Debug($"开始通知扩展宿主初始化前事件，已加载扩展：{_loadedExtensions.Count} 个。");
        var context = CreateLifecycleContext();
        foreach (var hook in _startupHooks)
        {
            hook.OnHostInitializing(context);
        }
    }

    public void NotifyHostInitialized()
    {
        ThrowIfDisposed();
        _logger.Debug($"开始通知扩展宿主初始化完成事件，已加载扩展：{_loadedExtensions.Count} 个。");
        var context = CreateLifecycleContext();
        foreach (var hook in _startupHooks)
        {
            hook.OnHostInitialized(context);
        }
    }

    public bool BeforeAccountAdd(AccountExtensionContext context)
    {
        ThrowIfDisposed();
        _logger.Debug($"开始执行添加账号前扩展钩子，账号：{context.Account.Name}，类型：{context.Account.Type}");
        foreach (var hook in _accountHooks)
        {
            hook.BeforeAccountAdd(context);
            if (context.IsCancelled)
            {
                _logger.Warn($"添加账号操作被扩展取消，账号：{context.Account.Name}，原因：{context.CancelReason ?? "未提供"}");
                return false;
            }
        }

        return true;
    }

    public void AfterAccountAdd(AccountExtensionContext context)
    {
        ThrowIfDisposed();
        foreach (var hook in _accountHooks)
        {
            hook.AfterAccountAdd(context);
        }
    }

    public void AfterAccountRemove(AccountExtensionContext context)
    {
        ThrowIfDisposed();
        foreach (var hook in _accountHooks)
        {
            hook.AfterAccountRemove(context);
        }
    }

    public void AfterAccountsLoaded(AccountCollectionExtensionContext context)
    {
        ThrowIfDisposed();
        foreach (var hook in _accountHooks)
        {
            hook.AfterAccountsLoaded(context);
        }
    }

    public bool BeforeManagedRootAdd(ManagedRootExtensionContext context)
    {
        ThrowIfDisposed();
        _logger.Debug($"开始执行添加游戏目录前扩展钩子，目录：{context.RootPath}");
        foreach (var hook in _versioningHooks)
        {
            hook.BeforeManagedRootAdd(context);
            if (context.IsCancelled)
            {
                _logger.Warn($"添加游戏目录操作被扩展取消，目录：{context.RootPath}，原因：{context.CancelReason ?? "未提供"}");
                return false;
            }
        }

        return true;
    }

    public void AfterManagedRootAdded(ManagedRootExtensionContext context)
    {
        ThrowIfDisposed();
        foreach (var hook in _versioningHooks)
        {
            hook.AfterManagedRootAdded(context);
        }
    }

    public void AfterSelectedRootChanged(ManagedRootExtensionContext context)
    {
        ThrowIfDisposed();
        foreach (var hook in _versioningHooks)
        {
            hook.AfterSelectedRootChanged(context);
        }
    }

    public void AfterVersionsScanned(VersionScanExtensionContext context)
    {
        ThrowIfDisposed();
        foreach (var hook in _versioningHooks)
        {
            hook.AfterVersionsScanned(context);
        }
    }

    public void AfterParentTaskAdded(TaskExtensionContext context)
    {
        ThrowIfDisposed();
        foreach (var hook in _taskHooks)
        {
            hook.AfterParentTaskAdded(context);
        }
    }

    public void LoadExtensions()
    {
        ThrowIfDisposed();
        _logger.Info($"开始加载扩展，宿主版本：{HostVersion}，扩展目录：{ExtensionsDirectory}");
        ReleaseLoadedExtensions();

        Directory.CreateDirectory(ExtensionsDirectory);

        var loadResults = new List<ExtensionLoadResult>();
        var scanResult = _packageReader.ScanPackages(ExtensionsDirectory);
        _logger.Info($"扩展扫描结果：有效 {scanResult.Packages.Count} 个，无效 {scanResult.InvalidPackages.Count} 个。");
        foreach (var invalidPackage in scanResult.InvalidPackages)
        {
            _logger.Warn($"无效扩展包已跳过：{invalidPackage.PackagePath}，原因：{invalidPackage.Message}");
            loadResults.Add(new ExtensionLoadResult
            {
                ExtensionId = Path.GetFileNameWithoutExtension(invalidPackage.PackagePath),
                PackagePath = invalidPackage.PackagePath,
                Status = ExtensionLoadStatus.SkippedInvalidPackage,
                Message = invalidPackage.Message
            });
        }

        var resolutionResult = _resolver.Resolve(scanResult.Packages, HostVersion);
        _logger.Info($"扩展依赖解析结果：可加载 {resolutionResult.ResolvedExtensions.Count} 个，已跳过 {resolutionResult.UnresolvedExtensions.Count} 个。");
        foreach (var unresolvedExtension in resolutionResult.UnresolvedExtensions)
        {
            _logger.Warn($"扩展已跳过：{unresolvedExtension.ExtensionId} {unresolvedExtension.Version ?? "未知版本"}，状态：{unresolvedExtension.Status}，原因：{unresolvedExtension.Message}");
            loadResults.Add(new ExtensionLoadResult
            {
                ExtensionId = unresolvedExtension.ExtensionId,
                Version = unresolvedExtension.Version,
                PackagePath = unresolvedExtension.PackagePath,
                Status = unresolvedExtension.Status,
                Message = unresolvedExtension.Message
            });
        }

        foreach (var resolvedExtension in resolutionResult.ResolvedExtensions)
        {
            var manifest = resolvedExtension.Package.Manifest;
            if (_disabledExtensionIds.Contains(manifest.Id))
            {
                _logger.Info($"扩展已被配置禁用，跳过加载：{manifest.Name} ({manifest.Id})");
                loadResults.Add(new ExtensionLoadResult
                {
                    ExtensionId = manifest.Id,
                    Version = manifest.Version,
                    PackagePath = resolvedExtension.Package.PackagePath,
                    Status = ExtensionLoadStatus.SkippedDisabled,
                    Message = $"扩展 {manifest.Id} 已被配置禁用。"
                });
                continue;
            }

            if (resolvedExtension.HardDependencies.Any(dependency =>
                    !_loadedExtensions.ContainsKey(dependency.Package.Manifest.Id)))
            {
                _logger.Warn($"扩展硬依赖加载失败，跳过当前扩展：{manifest.Name} ({manifest.Id})");
                loadResults.Add(new ExtensionLoadResult
                {
                    ExtensionId = manifest.Id,
                    Version = manifest.Version,
                    PackagePath = resolvedExtension.Package.PackagePath,
                    Status = ExtensionLoadStatus.SkippedMissingHardDependency,
                    Message = $"扩展 {manifest.Id} 的硬依赖加载失败，已跳过。"
                });
                continue;
            }

            try
            {
                _logger.Info($"准备加载扩展：{manifest.Name} ({manifest.Id})，版本：{manifest.Version}，硬依赖：{resolvedExtension.HardDependencies.Count} 个，软依赖：{resolvedExtension.SoftDependencies.Count} 个。");
                var loadedExtension = _loader.Load(new LMCExtensionLoadRequest
                {
                    HostVersion = HostVersion,
                    ExtensionsDirectory = ExtensionsDirectory,
                    ResolvedExtension = resolvedExtension,
                    HardDependencyCacheDirectories = resolvedExtension.HardDependencies
                        .Select(dependency => _loadedExtensions[dependency.Package.Manifest.Id].CacheDirectory)
                        .ToList()
                        .AsReadOnly(),
                    SoftDependencyCacheDirectories = resolvedExtension.SoftDependencies
                        .Where(dependency => _loadedExtensions.ContainsKey(dependency.Package.Manifest.Id))
                        .Select(dependency => _loadedExtensions[dependency.Package.Manifest.Id].CacheDirectory)
                        .ToList()
                        .AsReadOnly(),
                    LoggerFactory = _loggerFactory,
                    UIApi = _uiApi
                });

                RegisterHooks(loadedExtension.Instance);
                _loadedExtensions[manifest.Id] = loadedExtension;
                _logger.Debug($"扩展钩子注册完成：{manifest.Name} ({manifest.Id})");

                loadResults.Add(new ExtensionLoadResult
                {
                    ExtensionId = manifest.Id,
                    Version = manifest.Version,
                    PackagePath = resolvedExtension.Package.PackagePath,
                    Status = ExtensionLoadStatus.Loaded,
                    Message = $"扩展 {manifest.Id} 加载成功。"
                });
            }
            catch (Exception ex)
            {
                _logger.Error($"扩展加载失败：{manifest.Name} ({manifest.Id})", ex);
                loadResults.Add(new ExtensionLoadResult
                {
                    ExtensionId = manifest.Id,
                    Version = manifest.Version,
                    PackagePath = resolvedExtension.Package.PackagePath,
                    Status = ExtensionLoadStatus.FailedToLoad,
                    Message = ex.Message
                });
            }
        }

        LoadResults = loadResults.AsReadOnly();
        var loadedCount = LoadResults.Count(result => result.Status == ExtensionLoadStatus.Loaded);
        var skippedCount = LoadResults.Count(result => result.Status != ExtensionLoadStatus.Loaded && result.Status != ExtensionLoadStatus.FailedToLoad);
        var failedCount = LoadResults.Count(result => result.Status == ExtensionLoadStatus.FailedToLoad);
        _logger.Info($"扩展加载流程结束：成功 {loadedCount} 个，跳过 {skippedCount} 个，失败 {failedCount} 个。");
    }

    private void RegisterHooks(ILMCExtension extension)
    {
        if (extension is IStartupExtensionHooks startupExtensionHooks)
        {
            _startupHooks.Add(startupExtensionHooks);
        }

        if (extension is IAccountExtensionHooks accountExtensionHooks)
        {
            _accountHooks.Add(accountExtensionHooks);
        }

        if (extension is IVersioningExtensionHooks versioningExtensionHooks)
        {
            _versioningHooks.Add(versioningExtensionHooks);
        }

        if (extension is ITaskExtensionHooks taskExtensionHooks)
        {
            _taskHooks.Add(taskExtensionHooks);
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _logger.Info("开始释放扩展宿主及已加载的扩展资源。");
        ReleaseLoadedExtensions();
        _disposed = true;
        GC.SuppressFinalize(this);
    }

    private void ReleaseLoadedExtensions()
    {
        if (_loadedExtensions.Count > 0)
        {
            _logger.Debug($"准备卸载已加载扩展：{_loadedExtensions.Count} 个。");
        }

        _startupHooks.Clear();
        _accountHooks.Clear();
        _versioningHooks.Clear();
        _taskHooks.Clear();

        foreach (var loadContext in _loadedExtensions.Values.Select(value => value.LoadContext))
        {
            loadContext.Unload();
        }

        _loadedExtensions.Clear();
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        if (!_disposed)
        {
            _logger.Debug("扩展宿主内部状态已重置。");
        }
    }

    private static LMCExtensionHost CreateEmptyHost()
    {
        return new LMCExtensionHost(new LMCExtensionHostOptions
        {
            HostVersion = "0.0.0",
            ExtensionsDirectory = string.Empty
        });
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }

    private LifecycleExtensionContext CreateLifecycleContext()
    {
        return new LifecycleExtensionContext
        {
            HostVersion = HostVersion,
            ExtensionsDirectory = ExtensionsDirectory,
            LoadedExtensionCount = _loadedExtensions.Count
        };
    }
}
