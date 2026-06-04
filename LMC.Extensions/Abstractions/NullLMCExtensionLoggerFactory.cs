namespace LMC.Extensions.Abstractions;

internal sealed class NullLMCExtensionLoggerFactory : ILMCExtensionLoggerFactory
{
    public static NullLMCExtensionLoggerFactory Instance { get; } = new();

    private NullLMCExtensionLoggerFactory()
    {
    }

    public ILMCExtensionLogger CreateLogger(string category)
    {
        return NullLMCExtensionLogger.Instance;
    }

    private sealed class NullLMCExtensionLogger : ILMCExtensionLogger
    {
        public static NullLMCExtensionLogger Instance { get; } = new();

        public void Debug(string message)
        {
        }

        public void Info(string message)
        {
        }

        public void Warn(string message)
        {
        }

        public void Error(string message, Exception? exception = null)
        {
        }
    }
}
