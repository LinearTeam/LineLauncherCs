namespace LMC.Extensions.Hooks.Context;

public class AccountCollectionExtensionContext
{
    public required IReadOnlyList<ExtensionAccountInfo> Accounts { get; init; }
}
