using LMC.Extensions.Abstractions;
using LMC.Extensions.Resolution;
using LMC.Extensions.UI;

namespace LMC.Extensions.Loading;

public class LMCExtensionLoadRequest
{
    public required string HostVersion { get; init; }

    public required string ExtensionsDirectory { get; init; }

    public required ResolvedExtension ResolvedExtension { get; init; }

    public required IReadOnlyList<string> HardDependencyCacheDirectories { get; init; }

    public required IReadOnlyList<string> SoftDependencyCacheDirectories { get; init; }

    public required ILMCExtensionLoggerFactory LoggerFactory { get; init; }

    public required IUIExtensionApi UIApi { get; init; }
}
