namespace LMC.Extensions.Packaging;

public class ExtensionManifest
{
    public required string Id { get; init; }

    public required string Name { get; init; }

    public required string Version { get; init; }

    public required string EntryAssembly { get; init; }

    public required string EntryType { get; init; }

    public string? LauncherVersionRange { get; init; }

    public IReadOnlyList<ExtensionDependencyDescriptor> Dependencies { get; init; } = [];

    public string? Description { get; init; }

    public IReadOnlyList<string> Authors { get; init; } = [];
}
