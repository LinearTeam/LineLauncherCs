namespace LMC.Extensions.Hooks.Context;

public class LifecycleExtensionContext
{
    public required string HostVersion { get; init; }

    public required string ExtensionsDirectory { get; init; }

    public int LoadedExtensionCount { get; init; }
}
