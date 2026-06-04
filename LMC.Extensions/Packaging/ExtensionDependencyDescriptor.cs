namespace LMC.Extensions.Packaging;

public class ExtensionDependencyDescriptor
{
    public required string Id { get; init; }

    public required string VersionRange { get; init; }

    public required string Kind { get; init; }

    public bool IsHardDependency =>
        string.Equals(Kind, "hard", StringComparison.OrdinalIgnoreCase);
}
