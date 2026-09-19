using LMC.Extensions.Hooks.Context;

namespace LMC.Extensions.Hooks;

public interface IStartupExtensionHooks
{
    void OnHostInitializing(LifecycleExtensionContext context);

    void OnHostInitialized(LifecycleExtensionContext context);
}
