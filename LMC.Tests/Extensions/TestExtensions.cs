using LMC.Extensions.Abstractions;
using LMC.Extensions.Hooks;
using LMC.Extensions.Hooks.Context;
using LMC.Extensions.UI;
using LMCUI.I18n;
using LMCUI.Pages.LaunchPage;

namespace LMC.Tests.Extensions;

public sealed class ContextCaptureExtension : ILMCExtension
{
    public void Initialize(ILMCExtensionContext context)
    {
        WriteProbe(
            $"context:{context.ExtensionId}|{context.ExtensionVersion}|{context.HostVersion}|{context.DataDirectory}|{context.CacheDirectory}");
        WriteProbe($"hard:{string.Join(",", context.HardDependencies)}");
        WriteProbe($"soft:{string.Join(",", context.SoftDependencies)}");
    }

    private static void WriteProbe(string value)
    {
        var probePath = Environment.GetEnvironmentVariable("LMC_TEST_PROBE_PATH");
        if (!string.IsNullOrWhiteSpace(probePath))
        {
            File.AppendAllLines(probePath, [value]);
        }
    }
}

public sealed class NonExtensionEntryPoint
{
}

public sealed class SmokeTestExtension :
    ILMCExtension,
    IUIExtension,
    IStartupExtensionHooks,
    IAccountExtensionHooks,
    IVersioningExtensionHooks,
    ITaskExtensionHooks
{
    public void Initialize(ILMCExtensionContext context)
    {
        context.Logger.Info($"Initialized {context.ExtensionId}");
    }

    public void RegisterUI(IUIExtensionApi api)
    {
        api.RegisterPage(new UIExtensionPageRegistration
        {
            PageType = typeof(LaunchPage),
            StaticTag = "SmokeExtensionPage"
        });
        api.RegisterNavigationItem(new UIExtensionNavigationItem
        {
            Tag = "SmokeExtensionPage",
            Title = I18nManager.Instance.GetString("MainWindow.NavItems.LMCExampleExtension")
        });
    }

    public void OnHostInitializing(LifecycleExtensionContext context)
    {
        WriteProbe("host_initializing");
    }

    public void OnHostInitialized(LifecycleExtensionContext context)
    {
        WriteProbe("host_initialized");
    }

    public void BeforeAccountAdd(AccountExtensionContext context)
    {
        WriteProbe($"before_account_add:{context.Account.Name}");
    }

    public void AfterAccountAdd(AccountExtensionContext context)
    {
        WriteProbe("after_account_add");
    }

    public void AfterAccountRemove(AccountExtensionContext context)
    {
        WriteProbe("after_account_remove");
    }

    public void AfterAccountsLoaded(AccountCollectionExtensionContext context)
    {
        WriteProbe("after_accounts_loaded");
    }

    public void BeforeManagedRootAdd(ManagedRootExtensionContext context)
    {
        WriteProbe("before_managed_root_add");
    }

    public void AfterManagedRootAdded(ManagedRootExtensionContext context)
    {
        WriteProbe("after_managed_root_added");
    }

    public void AfterSelectedRootChanged(ManagedRootExtensionContext context)
    {
        WriteProbe("after_selected_root_changed");
    }

    public void AfterVersionsScanned(VersionScanExtensionContext context)
    {
        WriteProbe("after_versions_scanned");
    }

    public void AfterParentTaskAdded(TaskExtensionContext context)
    {
        WriteProbe("after_parent_task_added");
    }

    private static void WriteProbe(string value)
    {
        var probePath = Environment.GetEnvironmentVariable("LMC_TEST_PROBE_PATH");
        if (!string.IsNullOrWhiteSpace(probePath))
        {
            File.AppendAllLines(probePath, [value]);
        }
    }
}

internal sealed class TestHookExtension(string name, List<string> events) :
    ILMCExtension,
    IStartupExtensionHooks,
    IAccountExtensionHooks,
    IVersioningExtensionHooks,
    ITaskExtensionHooks
{
    public bool CancelAccountAdd { get; init; }

    public bool CancelManagedRootAdd { get; init; }

    public void Initialize(ILMCExtensionContext context)
    {
        events.Add($"{name}.Initialize");
    }

    public void OnHostInitializing(LifecycleExtensionContext context)
    {
        events.Add($"{name}.HostInitializing");
    }

    public void OnHostInitialized(LifecycleExtensionContext context)
    {
        events.Add($"{name}.HostInitialized");
    }

    public void BeforeAccountAdd(AccountExtensionContext context)
    {
        events.Add($"{name}.BeforeAccountAdd");
        if (CancelAccountAdd)
        {
            context.Cancel(name);
        }
    }

    public void AfterAccountAdd(AccountExtensionContext context)
    {
        events.Add($"{name}.AfterAccountAdd");
    }

    public void AfterAccountRemove(AccountExtensionContext context)
    {
        events.Add($"{name}.AfterAccountRemove");
    }

    public void AfterAccountsLoaded(AccountCollectionExtensionContext context)
    {
        events.Add($"{name}.AfterAccountsLoaded");
    }

    public void BeforeManagedRootAdd(ManagedRootExtensionContext context)
    {
        events.Add($"{name}.BeforeManagedRootAdd");
        if (CancelManagedRootAdd)
        {
            context.Cancel(name);
        }
    }

    public void AfterManagedRootAdded(ManagedRootExtensionContext context)
    {
        events.Add($"{name}.AfterManagedRootAdded");
    }

    public void AfterSelectedRootChanged(ManagedRootExtensionContext context)
    {
        events.Add($"{name}.AfterSelectedRootChanged");
    }

    public void AfterVersionsScanned(VersionScanExtensionContext context)
    {
        events.Add($"{name}.AfterVersionsScanned");
    }

    public void AfterParentTaskAdded(TaskExtensionContext context)
    {
        events.Add($"{name}.AfterParentTaskAdded");
    }
}
