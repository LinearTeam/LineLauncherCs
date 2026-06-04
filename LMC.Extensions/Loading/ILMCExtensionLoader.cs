namespace LMC.Extensions.Loading;

public interface ILMCExtensionLoader
{
    LoadedLMCExtension Load(LMCExtensionLoadRequest request);
}
