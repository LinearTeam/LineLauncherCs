namespace LMC.Extensions.Hooks.Context;

public class AccountExtensionContext
{
    public required ExtensionAccountInfo Account { get; init; }

    public bool IsCancelled { get; private set; }

    public string? CancelReason { get; private set; }

    public void Cancel(string? reason = null)
    {
        IsCancelled = true;
        CancelReason = reason;
    }
}
