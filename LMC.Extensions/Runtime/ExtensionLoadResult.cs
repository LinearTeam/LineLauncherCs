namespace LMC.Extensions.Runtime;

public class ExtensionLoadResult
{
    public required string ExtensionId { get; init; }

    public string? Version { get; init; }

    public string? PackagePath { get; init; }

    public required ExtensionLoadStatus Status { get; init; }

    public required string Message { get; init; }
}
