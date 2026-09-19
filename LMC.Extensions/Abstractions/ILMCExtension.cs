namespace LMC.Extensions.Abstractions;

public interface ILMCExtension
{
    void Initialize(ILMCExtensionContext context);
}
