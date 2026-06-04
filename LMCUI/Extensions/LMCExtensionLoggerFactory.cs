using System;
using LMC.Basic.Logging;
using LMC.Extensions.Abstractions;

namespace LMCUI.Extensions;

internal sealed class LMCExtensionLoggerFactory : ILMCExtensionLoggerFactory
{
    public ILMCExtensionLogger CreateLogger(string category)
    {
        return new LMCExtensionLogger(new Logger(category));
    }

    private sealed class LMCExtensionLogger(Logger logger) : ILMCExtensionLogger
    {
        private readonly Logger _logger = logger;

        public void Debug(string message)
        {
            _logger.Debug(message);
        }

        public void Info(string message)
        {
            _logger.Info(message);
        }

        public void Warn(string message)
        {
            _logger.Warn(message);
        }

        public void Error(string message, Exception? exception = null)
        {
            if (exception == null)
            {
                _logger.Error(message);
                return;
            }

            _logger.Error($"{message}\n{exception}");
        }
    }
}
