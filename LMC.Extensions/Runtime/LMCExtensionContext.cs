using LMC.Extensions.Abstractions;
using LMC.Extensions.UI;

namespace LMC.Extensions.Runtime;

internal sealed class LMCExtensionContext : ILMCExtensionContext
{
    public required string ExtensionId { get; init; }

    public required string ExtensionName { get; init; }

    public required string ExtensionVersion { get; init; }

    public required string HostVersion { get; init; }

    public required string DataDirectory { get; init; }

    public required string CacheDirectory { get; init; }

    public required IReadOnlyList<string> HardDependencies { get; init; }

    public required IReadOnlyList<string> SoftDependencies { get; init; }

    public required ILMCExtensionLogger Logger { get; init; }

    public required IUIExtensionApi UI { get; init; }
}
