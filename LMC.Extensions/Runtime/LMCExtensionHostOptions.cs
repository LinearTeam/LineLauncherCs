using LMC.Extensions.Abstractions;
using LMC.Extensions.UI;

namespace LMC.Extensions.Runtime;

public class LMCExtensionHostOptions
{
    public required string HostVersion { get; init; }

    public required string ExtensionsDirectory { get; init; }

    public IReadOnlyList<string> DisabledExtensionIds { get; init; } = [];

    public ILMCExtensionLoggerFactory? LoggerFactory { get; init; }

    public IUIExtensionApi? UIApi { get; init; }
}
