using LMC.Extensions.Runtime;

namespace LMC.Extensions.Resolution;

public class UnresolvedExtension
{
    public required string ExtensionId { get; init; }

    public string? Version { get; init; }

    public required ExtensionLoadStatus Status { get; init; }

    public required string Message { get; init; }

    public string? PackagePath { get; init; }
}
