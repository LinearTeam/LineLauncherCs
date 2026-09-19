using LMC.Extensions.Packaging;

namespace LMC.Extensions.Resolution;

public interface IExtensionResolver
{
    ExtensionResolutionResult Resolve(IReadOnlyList<ExtensionPackageInfo> packages, string hostVersion);
}
