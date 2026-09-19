using LMC.Extensions.Hooks.Context;

namespace LMC.Extensions.Hooks;

public interface ITaskExtensionHooks
{
    void AfterParentTaskAdded(TaskExtensionContext context);
}
