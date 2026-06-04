using LMC.Extensions.Packaging;

namespace LMC.Extensions.Resolution;

public class ResolvedExtension
{
    public required ExtensionPackageInfo Package { get; init; }

    public required IReadOnlyList<ResolvedExtension> HardDependencies { get; init; }

    public required IReadOnlyList<ResolvedExtension> SoftDependencies { get; init; }
}
