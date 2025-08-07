namespace LMC.Basic.Logging;

using System;
using System.IO;
using Configs;
using NLog;
using NLog.Config;
using NLog.Targets;

public class Logger
{
    private static bool s_isConfigured;
    private readonly NLog.Logger _nlogLogger;
    public static string LoggerVersion = "L2A6";
    public static bool DebugMode = false;

    public Logger(string module, string filePath = "not")
    {
        EnsureConfigured();
        _nlogLogger = LogManager.GetLogger(module);
    }

    private static void EnsureConfigured()
    {
        if (s_isConfigured) return;
            
        lock (typeof(Logger))
        {
            if (s_isConfigured) return;

            var config = new LoggingConfiguration();
            var logDir = Path.Combine(Current.LMCPath, "logs");
            Directory.CreateDirectory(logDir);

            var appConfig = Current.Config;
            LogLevel configLogLevel = appConfig?.LogLevel ?? LogLevel.Info;
            NLog.LogLevel minLogLevel = MapToNLogLevel(configLogLevel);
            
            var datedTarget = new FileTarget("datedTarget")
            {
                FileName = Path.Combine(logDir, "${date:format=yyyy-MM-dd}-${cached:${date}:cached=true:inner=${counter:DailyCounter}.log}"),
                Layout = "${longdate} [${level}] [${logger}] ${message}",
                // ArchiveOldFileOnStartup = true,
                ArchiveAboveSize = 10485760,
                ArchiveSuffixFormat = "#",
                MaxArchiveFiles = 100,
                ArchiveEvery = FileArchivePeriod.Day
            };

            var latestTarget = new FileTarget("latestTarget")
            {
                FileName = Path.Combine(logDir, "latest.log"),
                Layout = "${longdate} [${level}] [${logger}] ${message}",
            };

            var consoleTarget = new ConsoleTarget("console")
            {
                Layout = "${longdate} [${level}] [${logger}] ${message}"
            };

            config.AddRule(minLogLevel, NLog.LogLevel.Fatal, datedTarget);
            config.AddRule(minLogLevel, NLog.LogLevel.Fatal, latestTarget);
            config.AddRule(minLogLevel, NLog.LogLevel.Fatal, consoleTarget);

            LogManager.Configuration = config;
            s_isConfigured = true;
        }
    }

    private static NLog.LogLevel MapToNLogLevel(LogLevel level)
    {
        return level switch
        {
            LogLevel.Off => NLog.LogLevel.Off,
            LogLevel.Error => NLog.LogLevel.Error,
            LogLevel.Warn => NLog.LogLevel.Warn,
            LogLevel.Info => NLog.LogLevel.Info,
            LogLevel.Debug => NLog.LogLevel.Debug,
            _ => NLog.LogLevel.Info
        };
    }

    public void Info(string msg) => _nlogLogger.Info(msg);
    public void Error(string msg) => _nlogLogger.Error(msg);
    public void Warn(string msg) => _nlogLogger.Warn(msg);
        
    public void Error(Exception e, string func)
    {
        _nlogLogger.Error($"An exception occurred when {func}:\n{e}");
    }

    public void Debug(string msg)
    {
        if (DebugMode)
        {
            _nlogLogger.Debug(msg);
        }
    }

    public void Close() => LogManager.Shutdown();
        
    ~Logger() => Close();
}