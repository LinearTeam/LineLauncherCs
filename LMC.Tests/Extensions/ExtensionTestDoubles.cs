using System.Runtime.Loader;
using LMC.Extensions.Abstractions;
using LMC.Extensions.Loading;
using LMC.Extensions.Packaging;
using LMC.Extensions.Resolution;
using LMC.Extensions.UI;

namespace LMC.Tests.Extensions;

internal sealed class RecordingUIExtensionApi : IUIExtensionApi
{
    public List<UIExtensionNavigationItem> NavigationItems { get; } = [];

    public List<UIExtensionPageRegistration> PageRegistrations { get; } = [];

    public void RegisterPage(UIExtensionPageRegistration registration)
    {
        PageRegistrations.Add(registration);
    }

    public void RegisterNavigationItem(UIExtensionNavigationItem navigationItem)
    {
        NavigationItems.Add(navigationItem);
    }
}

internal sealed class TestLoggerFactory : ILMCExtensionLoggerFactory
{
    private readonly Dictionary<string, TestLogger> _loggers = new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyDictionary<string, TestLogger> Loggers => _loggers;

    public ILMCExtensionLogger CreateLogger(string category)
    {
        if (_loggers.TryGetValue(category, out var logger))
        {
            return logger;
        }

        logger = new TestLogger();
        _loggers[category] = logger;
        return logger;
    }
}

internal sealed class TestLogger : ILMCExtensionLogger
{
    public List<string> DebugMessages { get; } = [];

    public List<string> InfoMessages { get; } = [];

    public List<string> WarnMessages { get; } = [];

    public List<string> ErrorMessages { get; } = [];

    public void Debug(string message)
    {
        DebugMessages.Add(message);
    }

    public void Info(string message)
    {
        InfoMessages.Add(message);
    }

    public void Warn(string message)
    {
        WarnMessages.Add(message);
    }

    public void Error(string message, Exception? exception = null)
    {
        ErrorMessages.Add(exception == null ? message : $"{message} :: {exception.Message}");
    }
}

internal sealed class FakePackageReader(ExtensionPackageScanResult result) : IExtensionPackageReader
{
    public int ScanCount { get; private set; }

    public ExtensionPackageScanResult ScanPackages(string extensionDirectory)
    {
        ScanCount++;
        return result;
    }
}

internal sealed class FakeResolver(ExtensionResolutionResult result) : IExtensionResolver
{
    public int ResolveCount { get; private set; }

    public ExtensionResolutionResult Resolve(IReadOnlyList<ExtensionPackageInfo> packages, string hostVersion)
    {
        ResolveCount++;
        return result;
    }
}

internal sealed class FakeLoader(Func<LMCExtensionLoadRequest, LoadedLMCExtension> loadFunc) : ILMCExtensionLoader
{
    private readonly Func<LMCExtensionLoadRequest, LoadedLMCExtension> _loadFunc = loadFunc;

    public List<LMCExtensionLoadRequest> Requests { get; } = [];

    public LoadedLMCExtension Load(LMCExtensionLoadRequest request)
    {
        Requests.Add(request);
        return _loadFunc(request);
    }
}

internal sealed class TrackingAssemblyLoadContext : AssemblyLoadContext
{
    public TrackingAssemblyLoadContext() : base($"Tracking::{Guid.NewGuid():N}", isCollectible: true)
    {
        Unloading += _ => UnloadingRaised = true;
    }

    public bool UnloadingRaised { get; private set; }
}
