namespace LMC.Extensions.Abstractions;

public interface ILMCExtensionLogger
{
    void Debug(string message);

    void Info(string message);

    void Warn(string message);

    void Error(string message, Exception? exception = null);
}
