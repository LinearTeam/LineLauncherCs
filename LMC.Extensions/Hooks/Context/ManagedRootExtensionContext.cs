namespace LMC.Extensions.Hooks.Context;

public class ManagedRootExtensionContext
{
    public required string RootPath { get; init; }

    public bool IsCancelled { get; private set; }

    public string? CancelReason { get; private set; }

    public void Cancel(string? reason = null)
    {
        IsCancelled = true;
        CancelReason = reason;
    }
}
