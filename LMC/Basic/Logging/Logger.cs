// Copyright 2025-2026 LinearTeam
// 
//    Licensed under the Apache License, Version 2.0 (the "License");
//    you may not use this file except in compliance with the License.
//    You may obtain a copy of the License at
// 
//        http://www.apache.org/licenses/LICENSE-2.0
// 
//    Unless required by applicable law or agreed to in writing, software
//    distributed under the License is distributed on an "AS IS" BASIS,
//    WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//    See the License for the specific language governing permissions and
//    limitations under the License.

using System.Collections.Concurrent;
using LMC.Basic.Configs;

namespace LMC.Basic.Logging;

using System;
using System.IO;
using NLog;
using NLog.Config;
using NLog.Targets;

public class Logger
{
    private static bool s_isConfigured;
    private readonly NLog.Logger _nlogLogger;
    public static string LoggerVersion = "L2A6";
    public static bool DebugMode = false;

    public Logger(string module)
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
    private static string ReplaceSensitiveData(string msg)
    {
        foreach (var kv in SecretsManager.SensitiveData)
        {
            if(!string.IsNullOrWhiteSpace(kv.Key)) msg = msg.Replace(kv.Key, kv.Value);
        }
        return msg;
    }
    public void Info(string msg) => _nlogLogger.Info(ReplaceSensitiveData(msg));
    public void Error(string msg) => _nlogLogger.Error(ReplaceSensitiveData(msg));
    public void Warn(string msg) => _nlogLogger.Warn(ReplaceSensitiveData(msg));
        
    public void Error(Exception e, string func)
    {
        Error($"在执行操作 {func} 时遇到错误:\n{e}");
    }

    public void Debug(string msg)
    {
        if (DebugMode)
        {
            _nlogLogger.Debug(ReplaceSensitiveData(msg));
        }
    }

    public void Close() => LogManager.Shutdown();
        
    ~Logger() => Close();
}