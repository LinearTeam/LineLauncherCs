using LMC.ExampleExtension.UI;
using LMC.Extensions.Abstractions;
using LMC.Extensions.Hooks;
using LMC.Extensions.Hooks.Context;
using LMC.Extensions.UI;
using LMCUI.I18n;

namespace LMC.ExampleExtension.Runtime;

public sealed class ExampleExtension :
    ILMCExtension,
    IUIExtension,
    IStartupExtensionHooks,
    IAccountExtensionHooks,
    IVersioningExtensionHooks,
    ITaskExtensionHooks
{
    public void Initialize(ILMCExtensionContext context)
    {
        ExampleExtensionState.Initialize(context);
    }

    public void RegisterUI(IUIExtensionApi api)
    {
        api.RegisterPage(new UIExtensionPageRegistration
        {
            PageType = typeof(ExampleExtensionPage),
            StaticTag = "LMCExampleExtensionPage"
        });
        api.RegisterNavigationItem(new UIExtensionNavigationItem
        {
            Tag = "LMCExampleExtensionPage",
            Title = I18nManager.Instance.GetString("MainWindow.NavItems.LMCExampleExtension")
        });
    }

    public void OnHostInitializing(LifecycleExtensionContext context)
    {
        ExampleExtensionState.RecordEvent("HostInitializing");
    }

    public void OnHostInitialized(LifecycleExtensionContext context)
    {
        ExampleExtensionState.RecordEvent("HostInitialized");
    }

    public void BeforeAccountAdd(AccountExtensionContext context)
    {
        ExampleExtensionState.RecordEvent("BeforeAccountAdd");
    }

    public void AfterAccountAdd(AccountExtensionContext context)
    {
        ExampleExtensionState.RecordEvent("AfterAccountAdd");
    }

    public void AfterAccountRemove(AccountExtensionContext context)
    {
        ExampleExtensionState.RecordEvent("AfterAccountRemove");
    }

    public void AfterAccountsLoaded(AccountCollectionExtensionContext context)
    {
        ExampleExtensionState.RecordEvent("AfterAccountsLoaded");
    }

    public void BeforeManagedRootAdd(ManagedRootExtensionContext context)
    {
        ExampleExtensionState.RecordEvent("BeforeManagedRootAdd");
    }

    public void AfterManagedRootAdded(ManagedRootExtensionContext context)
    {
        ExampleExtensionState.RecordEvent("AfterManagedRootAdded");
    }

    public void AfterSelectedRootChanged(ManagedRootExtensionContext context)
    {
        ExampleExtensionState.RecordEvent("AfterSelectedRootChanged");
    }

    public void AfterVersionsScanned(VersionScanExtensionContext context)
    {
        ExampleExtensionState.RecordEvent("AfterVersionsScanned");
    }

    public void AfterParentTaskAdded(TaskExtensionContext context)
    {
        ExampleExtensionState.RecordEvent("AfterParentTaskAdded");
    }
}
