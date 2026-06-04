using LMC.Extensions.UI;

namespace LMC.Extensions.Abstractions;

public interface ILMCExtensionContext
{
    string ExtensionId { get; }

    string ExtensionName { get; }

    string ExtensionVersion { get; }

    string HostVersion { get; }

    string DataDirectory { get; }

    string CacheDirectory { get; }

    IReadOnlyList<string> HardDependencies { get; }

    IReadOnlyList<string> SoftDependencies { get; }

    ILMCExtensionLogger Logger { get; }

    IUIExtensionApi UI { get; }
}
