namespace LMC.Extensions.Abstractions;

public interface ILMCExtensionLoggerFactory
{
    ILMCExtensionLogger CreateLogger(string category);
}
