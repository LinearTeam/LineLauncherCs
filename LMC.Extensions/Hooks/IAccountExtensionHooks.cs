using LMC.Extensions.Hooks.Context;

namespace LMC.Extensions.Hooks;

public interface IAccountExtensionHooks
{
    void BeforeAccountAdd(AccountExtensionContext context);

    void AfterAccountAdd(AccountExtensionContext context);

    void AfterAccountRemove(AccountExtensionContext context);

    void AfterAccountsLoaded(AccountCollectionExtensionContext context);
}
