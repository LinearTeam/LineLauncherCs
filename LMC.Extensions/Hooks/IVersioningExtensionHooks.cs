using LMC.Extensions.Hooks.Context;

namespace LMC.Extensions.Hooks;

public interface IVersioningExtensionHooks
{
    void BeforeManagedRootAdd(ManagedRootExtensionContext context);

    void AfterManagedRootAdded(ManagedRootExtensionContext context);

    void AfterSelectedRootChanged(ManagedRootExtensionContext context);

    void AfterVersionsScanned(VersionScanExtensionContext context);
}
