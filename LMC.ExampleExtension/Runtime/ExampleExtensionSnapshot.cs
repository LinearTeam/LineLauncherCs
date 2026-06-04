namespace LMC.ExampleExtension.Runtime;

internal sealed class ExampleExtensionSnapshot
{
    public required string HostVersion { get; init; }

    public required string ExtensionVersion { get; init; }

    public required string LastEvent { get; init; }

    public required IReadOnlyDictionary<string, int> EventCounts { get; init; }
}
