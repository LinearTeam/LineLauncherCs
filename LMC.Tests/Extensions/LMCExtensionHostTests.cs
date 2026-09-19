using LMC.Extensions.Hooks.Context;
using LMC.Extensions.Loading;
using LMC.Extensions.Packaging;
using LMC.Extensions.Resolution;
using LMC.Extensions.Runtime;

namespace LMC.Tests.Extensions;

public class LMCExtensionHostTests
{
    [Fact]
    public void LoadExtensions_RespectsDisabledExtensionIds()
    {
        var package = ExtensionPackageBuilder.CreatePackageInfo("disabled.extension", "1.0.0", "[3.0.0,4.0.0)");
        var resolvedExtension = ExtensionPackageBuilder.CreateResolvedExtension(package);
        var fakeLoader = new FakeLoader(_ => throw new InvalidOperationException("Loader should not run for disabled extensions."));
        using var host = CreateHost(
            [resolvedExtension],
            fakeLoader,
            disabledExtensionIds: ["disabled.extension"]);

        host.LoadExtensions();

        Assert.Contains(host.LoadResults, result =>
            result.ExtensionId == "disabled.extension" &&
            result.Status == ExtensionLoadStatus.SkippedDisabled);
        Assert.Empty(fakeLoader.Requests);
    }

    [Fact]
    public void LoadExtensions_ContinuesLoadingAfterFailure()
    {
        var failingPackage = ExtensionPackageBuilder.CreatePackageInfo("failing.extension", "1.0.0", "[3.0.0,4.0.0)");
        var workingPackage = ExtensionPackageBuilder.CreatePackageInfo("working.extension", "1.0.0", "[3.0.0,4.0.0)");
        var failingExtension = ExtensionPackageBuilder.CreateResolvedExtension(failingPackage);
        var workingExtension = ExtensionPackageBuilder.CreateResolvedExtension(workingPackage);
        var events = new List<string>();
        var fakeLoader = new FakeLoader(request =>
        {
            if (request.ResolvedExtension.Package.Manifest.Id == "failing.extension")
            {
                throw new InvalidOperationException("boom");
            }

            return CreateLoadedExtension(request.ResolvedExtension, new TestHookExtension("Working", events));
        });

        using var host = CreateHost([failingExtension, workingExtension], fakeLoader);

        host.LoadExtensions();

        Assert.Contains(host.LoadResults, result =>
            result.ExtensionId == "failing.extension" &&
            result.Status == ExtensionLoadStatus.FailedToLoad);
        Assert.Contains(host.LoadResults, result =>
            result.ExtensionId == "working.extension" &&
            result.Status == ExtensionLoadStatus.Loaded);
    }

    [Fact]
    public void BeforeHooks_RespectCancellation()
    {
        var package = ExtensionPackageBuilder.CreatePackageInfo("hook.extension", "1.0.0", "[3.0.0,4.0.0)");
        var resolvedExtension = ExtensionPackageBuilder.CreateResolvedExtension(package);
        var events = new List<string>();
        var extension = new TestHookExtension("Hook", events)
        {
            CancelAccountAdd = true,
            CancelManagedRootAdd = true
        };

        using var host = CreateHost(
            [resolvedExtension],
            new FakeLoader(request => CreateLoadedExtension(request.ResolvedExtension, extension)));

        host.LoadExtensions();

        var accountContext = new AccountExtensionContext
        {
            Account = new ExtensionAccountInfo
            {
                Type = "Offline",
                Name = "Blocked"
            }
        };
        var managedRootContext = new ManagedRootExtensionContext
        {
            RootPath = "C:/games"
        };

        Assert.False(host.BeforeAccountAdd(accountContext));
        Assert.True(accountContext.IsCancelled);
        Assert.False(host.BeforeManagedRootAdd(managedRootContext));
        Assert.True(managedRootContext.IsCancelled);
        Assert.Contains("Hook.BeforeAccountAdd", events);
        Assert.Contains("Hook.BeforeManagedRootAdd", events);
    }

    [Fact]
    public void AfterHooks_DispatchInResolvedOrder()
    {
        var firstPackage = ExtensionPackageBuilder.CreatePackageInfo("first.extension", "1.0.0", "[3.0.0,4.0.0)");
        var secondPackage = ExtensionPackageBuilder.CreatePackageInfo("second.extension", "1.0.0", "[3.0.0,4.0.0)");
        var firstResolved = ExtensionPackageBuilder.CreateResolvedExtension(firstPackage);
        var secondResolved = ExtensionPackageBuilder.CreateResolvedExtension(secondPackage);
        var events = new List<string>();

        using var host = CreateHost(
            [firstResolved, secondResolved],
            new FakeLoader(request =>
            {
                var name = request.ResolvedExtension.Package.Manifest.Id == "first.extension" ? "First" : "Second";
                return CreateLoadedExtension(request.ResolvedExtension, new TestHookExtension(name, events));
            }));

        host.LoadExtensions();
        host.NotifyHostInitializing();
        host.NotifyHostInitialized();
        host.AfterAccountAdd(new AccountExtensionContext
        {
            Account = new ExtensionAccountInfo
            {
                Type = "Offline",
                Name = "Allowed"
            }
        });
        host.AfterManagedRootAdded(new ManagedRootExtensionContext
        {
            RootPath = "C:/games"
        });
        host.AfterParentTaskAdded(new TaskExtensionContext
        {
            ParentName = "Install"
        });

        Assert.Equal(
            [
                "First.HostInitializing",
                "Second.HostInitializing",
                "First.HostInitialized",
                "Second.HostInitialized",
                "First.AfterAccountAdd",
                "Second.AfterAccountAdd",
                "First.AfterManagedRootAdded",
                "Second.AfterManagedRootAdded",
                "First.AfterParentTaskAdded",
                "Second.AfterParentTaskAdded"
            ],
            events);
    }

    [Fact]
    public void Dispose_UnloadsLoadedExtensions()
    {
        var package = ExtensionPackageBuilder.CreatePackageInfo("dispose.extension", "1.0.0", "[3.0.0,4.0.0)");
        var resolvedExtension = ExtensionPackageBuilder.CreateResolvedExtension(package);
        var events = new List<string>();
        var loadContext = new TrackingAssemblyLoadContext();

        var host = CreateHost(
            [resolvedExtension],
            new FakeLoader(request => new LoadedLMCExtension
            {
                ResolvedExtension = request.ResolvedExtension,
                Instance = new TestHookExtension("Dispose", events),
                CacheDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")),
                LoadContext = loadContext
            }));

        host.LoadExtensions();
        host.Dispose();

        Assert.True(loadContext.UnloadingRaised);
    }

    [Fact]
    public void ResetCurrent_ReplacesCurrentHost()
    {
        var extensionsDirectory = Path.Combine(Path.GetTempPath(), "LMC.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(extensionsDirectory);

        try
        {
            var host = LMCExtensionHost.InitializeCurrent("3.0.0", extensionsDirectory);

            LMCExtensionHost.ResetCurrent();

            Assert.NotSame(host, LMCExtensionHost.Current);
            Assert.Equal("0.0.0", LMCExtensionHost.Current.HostVersion);
        }
        finally
        {
            if (Directory.Exists(extensionsDirectory))
            {
                Directory.Delete(extensionsDirectory, true);
            }
        }
    }

    private static LMCExtensionHost CreateHost(
        IReadOnlyList<ResolvedExtension> resolvedExtensions,
        ILMCExtensionLoader loader,
        IReadOnlyList<string>? disabledExtensionIds = null)
    {
        var packageReader = new FakePackageReader(new ExtensionPackageScanResult
        {
            Packages = resolvedExtensions.Select(extension => extension.Package).ToList().AsReadOnly()
        });
        var resolver = new FakeResolver(new ExtensionResolutionResult
        {
            ResolvedExtensions = resolvedExtensions,
            UnresolvedExtensions = []
        });

        return new LMCExtensionHost(
            new LMCExtensionHostOptions
            {
                HostVersion = "3.0.0",
                ExtensionsDirectory = Path.Combine(Path.GetTempPath(), "LMC.Tests", Guid.NewGuid().ToString("N")),
                DisabledExtensionIds = disabledExtensionIds ?? [],
                LoggerFactory = new TestLoggerFactory(),
                UIApi = new RecordingUIExtensionApi()
            },
            packageReader,
            resolver,
            loader);
    }

    private static LoadedLMCExtension CreateLoadedExtension(ResolvedExtension resolvedExtension, LMC.Extensions.Abstractions.ILMCExtension extension)
    {
        return new LoadedLMCExtension
        {
            ResolvedExtension = resolvedExtension,
            Instance = extension,
            CacheDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")),
            LoadContext = new TrackingAssemblyLoadContext()
        };
    }
}
