using LMC.Extensions.Abstractions;
using LMC.Extensions.Runtime;
using LMC.Extensions.UI;

namespace LMC.Extensions.Loading;

public sealed class LMCExtensionLoader(ILMCExtensionPackageExtractor packageExtractor) : ILMCExtensionLoader
{
    private readonly ILMCExtensionPackageExtractor _packageExtractor = packageExtractor;
    private readonly ILMCExtensionLogger _logger = NullLMCExtensionLoggerFactory.Instance.CreateLogger(nameof(LMCExtensionLoader));

    public LMCExtensionLoader(
        ILMCExtensionPackageExtractor packageExtractor,
        ILMCExtensionLogger? logger = null) : this(packageExtractor)
    {
        _logger = logger ?? NullLMCExtensionLoggerFactory.Instance.CreateLogger(nameof(LMCExtensionLoader));
    }

    public LoadedLMCExtension Load(LMCExtensionLoadRequest request)
    {
        var manifest = request.ResolvedExtension.Package.Manifest;
        _logger.Info($"开始加载扩展：{manifest.Id} {manifest.Version}");
        var cacheDirectory = _packageExtractor.Extract(request.ResolvedExtension.Package, request.ExtensionsDirectory);
        var entryAssemblyPath = Path.Combine(cacheDirectory, "lib", manifest.EntryAssembly);
        if (!File.Exists(entryAssemblyPath))
        {
            throw new FileNotFoundException("Entry assembly was not found in extracted package.", entryAssemblyPath);
        }

        var dependencyDirectories = request.HardDependencyCacheDirectories
            .Concat(request.SoftDependencyCacheDirectories)
            .Select(directory => Path.Combine(directory, "lib"))
            .ToList();
        _logger.Debug($"扩展 {manifest.Id} 的依赖程序集目录数量：{dependencyDirectories.Count}");

        var loadContext = new ExtensionAssemblyLoadContext(entryAssemblyPath, dependencyDirectories);

        try
        {
            var assembly = loadContext.LoadFromAssemblyPath(entryAssemblyPath);
            _logger.Debug($"扩展程序集已载入：{entryAssemblyPath}");
            var entryType = assembly.GetType(manifest.EntryType, throwOnError: false);
            if (entryType == null)
            {
                throw new TypeLoadException($"Entry type {manifest.EntryType} was not found.");
            }

            _logger.Debug($"扩展入口类型已解析：{manifest.EntryType}");

            if (Activator.CreateInstance(entryType) is not ILMCExtension extension)
            {
                throw new InvalidOperationException($"Entry type {manifest.EntryType} does not implement ILMCExtension.");
            }

            var extensionLogger = request.LoggerFactory.CreateLogger($"Extension.{manifest.Id}");
            extensionLogger.Info($"开始初始化扩展：{manifest.Name} ({manifest.Id})");
            var context = new LMCExtensionContext
            {
                ExtensionId = manifest.Id,
                ExtensionName = manifest.Name,
                ExtensionVersion = manifest.Version,
                HostVersion = request.HostVersion,
                DataDirectory = Path.Combine(request.ExtensionsDirectory, "data", manifest.Id),
                CacheDirectory = cacheDirectory,
                HardDependencies = request.ResolvedExtension.HardDependencies
                    .Select(dependency => dependency.Package.Manifest.Id)
                    .ToList()
                    .AsReadOnly(),
                SoftDependencies = request.ResolvedExtension.SoftDependencies
                    .Select(dependency => dependency.Package.Manifest.Id)
                    .ToList()
                    .AsReadOnly(),
                Logger = extensionLogger,
                UI = request.UIApi
            };

            Directory.CreateDirectory(context.DataDirectory);
            _logger.Debug($"扩展数据目录已就绪：{context.DataDirectory}");

            extension.Initialize(context);
            extensionLogger.Info($"扩展初始化完成：{manifest.Name} ({manifest.Id})");
            if (extension is IUIExtension uiExtension)
            {
                extensionLogger.Info($"检测到 UI 扩展能力，开始注册界面：{manifest.Name} ({manifest.Id})");
                uiExtension.RegisterUI(request.UIApi);
                extensionLogger.Info($"扩展界面注册完成：{manifest.Name} ({manifest.Id})");
            }

            extensionLogger.Info($"扩展加载成功：{manifest.Name} ({manifest.Id})");
            _logger.Info($"扩展加载完成：{manifest.Id} {manifest.Version}");

            return new LoadedLMCExtension
            {
                ResolvedExtension = request.ResolvedExtension,
                Instance = extension,
                CacheDirectory = cacheDirectory,
                LoadContext = loadContext
            };
        }
        catch (Exception ex)
        {
            _logger.Error($"扩展加载失败：{manifest.Id} {manifest.Version}", ex);
            loadContext.Unload();
            throw;
        }
    }
}
