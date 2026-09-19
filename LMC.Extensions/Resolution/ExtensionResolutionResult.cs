namespace LMC.Extensions.Resolution;

public class ExtensionResolutionResult
{
    public IReadOnlyList<ResolvedExtension> ResolvedExtensions { get; init; } = [];

    public IReadOnlyList<UnresolvedExtension> UnresolvedExtensions { get; init; } = [];
}
