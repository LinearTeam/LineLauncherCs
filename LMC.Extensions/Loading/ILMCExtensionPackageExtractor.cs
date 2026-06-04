using LMC.Extensions.Packaging;

namespace LMC.Extensions.Loading;

public interface ILMCExtensionPackageExtractor
{
    string Extract(ExtensionPackageInfo package, string extensionsDirectory);
}
