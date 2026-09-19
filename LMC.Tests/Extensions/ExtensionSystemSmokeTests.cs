using LMC.Extensions.Hooks.Context;
using LMC.Extensions.Runtime;
using LMCUI.Extensions;
using LMCUI.Navigation;
using LMCUI.Pages.LaunchPage;

namespace LMC.Tests.Extensions;

public class ExtensionSystemSmokeTests
{
    [Fact]
    public void InitializeCurrent_LoadsRealLextAndDispatchesHooks()
    {
        using var scope = new TestFileSystemScope();
        var extensionsDirectory = scope.CreateDirectory("extensions");
        var probePath = scope.GetPath("probe.log");
        var manifest = ExtensionPackageBuilder.CreateManifest(
            "smoke.extension",
            "1.0.0",
            Path.GetFileName(typeof(SmokeTestExtension).Assembly.Location),
            typeof(SmokeTestExtension).FullName!);
        ExtensionPackageBuilder.CreatePackage(
            Path.Combine(extensionsDirectory, "smoke.lext"),
            manifest,
            entryType: typeof(SmokeTestExtension));

        Environment.SetEnvironmentVariable("LMC_TEST_PROBE_PATH", probePath);

        try
        {
            var uiApi = new RecordingUIExtensionApi();
            using var host = LMCExtensionHost.InitializeCurrent(
                "3.0.0",
                extensionsDirectory,
                loggerFactory: new TestLoggerFactory(),
                uiApi: uiApi);

            host.NotifyHostInitializing();
            host.NotifyHostInitialized();
            host.BeforeAccountAdd(new AccountExtensionContext
            {
                Account = new ExtensionAccountInfo
                {
                    Type = "Offline",
                    Name = "Allowed"
                }
            });
            host.AfterAccountAdd(new AccountExtensionContext
            {
                Account = new ExtensionAccountInfo
                {
                    Type = "Offline",
                    Name = "Allowed"
                }
            });
            host.AfterAccountRemove(new AccountExtensionContext
            {
                Account = new ExtensionAccountInfo
                {
                    Type = "Offline",
                    Name = "Allowed"
                }
            });
            host.AfterAccountsLoaded(new AccountCollectionExtensionContext
            {
                Accounts =
                [
                    new ExtensionAccountInfo
                    {
                        Type = "Offline",
                        Name = "Allowed"
                    }
                ]
            });
            host.BeforeManagedRootAdd(new ManagedRootExtensionContext
            {
                RootPath = "C:/games"
            });
            host.AfterManagedRootAdded(new ManagedRootExtensionContext
            {
                RootPath = "C:/games"
            });
            host.AfterSelectedRootChanged(new ManagedRootExtensionContext
            {
                RootPath = "C:/games"
            });
            host.AfterVersionsScanned(new VersionScanExtensionContext
            {
                RootPath = "C:/games",
                VersionIds = ["1.20.1"]
            });
            host.AfterParentTaskAdded(new TaskExtensionContext
            {
                ParentName = "Install"
            });

            Assert.Contains(host.LoadResults, result =>
                result.ExtensionId == "smoke.extension" &&
                result.Status == ExtensionLoadStatus.Loaded);
            Assert.Single(uiApi.NavigationItems);
            Assert.Single(uiApi.PageRegistrations);

            var probeLines = File.ReadAllLines(probePath);
            Assert.Contains("host_initializing", probeLines);
            Assert.Contains("host_initialized", probeLines);
            Assert.Contains("before_account_add:Allowed", probeLines);
            Assert.Contains("after_account_add", probeLines);
            Assert.Contains("after_account_remove", probeLines);
            Assert.Contains("after_accounts_loaded", probeLines);
            Assert.Contains("before_managed_root_add", probeLines);
            Assert.Contains("after_managed_root_added", probeLines);
            Assert.Contains("after_selected_root_changed", probeLines);
            Assert.Contains("after_versions_scanned", probeLines);
            Assert.Contains("after_parent_task_added", probeLines);
        }
        finally
        {
            Environment.SetEnvironmentVariable("LMC_TEST_PROBE_PATH", null);
            LMCExtensionHost.ResetCurrent();
        }
    }

    [Fact]
    public void RegisterPage_RegistersIntoPageRegistry()
    {
        var api = new LMCExtensionUIApi();
        api.RegisterPage(new LMC.Extensions.UI.UIExtensionPageRegistration
        {
            PageType = typeof(LaunchPage),
            StaticTag = "ExtensionLaunchPage"
        });

        var registration = PageRegistry.Instance.GetByStaticTag("ExtensionLaunchPage");

        Assert.NotNull(registration);
        Assert.Equal(typeof(LaunchPage), registration!.PageType);
    }
}
