using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

namespace LMC.LifeCycle;

using Basic.Logging;

public static class CrashReportManager
{
    private static readonly ConcurrentDictionary<int, byte> s_reportedExceptions = new();
    private static readonly Lock s_installLock = new();
    private static bool s_installed;

    public static void InstallGlobalHandlers()
    {
        lock (s_installLock)
        {
            if (s_installed)
            {
                return;
            }

            AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
            TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
            s_installed = true;
        }
    }

    public static string? TryWriteReport(Exception exception, string source, bool isTerminating = false)
    {
        if (!TryRegisterException(exception))
        {
            return null;
        }

        try
        {
            var reportPath = WriteReport(
                exception,
                source,
                Current.LMCPath,
                Path.Combine(Current.LMCPath, "logs", "latest.log"),
                isTerminating);
            TryLogReportCreated(reportPath, source);
            return reportPath;
        }
        catch
        {
            return null;
        }
    }

    internal static string WriteReport(
        Exception exception,
        string source,
        string appDataPath,
        string latestLogPath,
        bool isTerminating = false,
        DateTimeOffset? timestamp = null)
    {
        var occurredAt = timestamp ?? DateTimeOffset.Now;
        var crashDirectory = Path.Combine(appDataPath, "crashes");
        Directory.CreateDirectory(crashDirectory);

        var reportPath = Path.Combine(
            crashDirectory,
            $"crash-{occurredAt:yyyyMMdd-HHmmss-fff}.txt");
        var reportContent = BuildReportContent(exception, source, latestLogPath, occurredAt, isTerminating);

        File.WriteAllText(reportPath, reportContent, Encoding.UTF8);
        return reportPath;
    }

    private static string BuildReportContent(
        Exception exception,
        string source,
        string latestLogPath,
        DateTimeOffset occurredAt,
        bool isTerminating)
    {
        var builder = new StringBuilder();
        builder.AppendLine("LineLauncherCs Crash Report");
        builder.AppendLine("===========================");
        builder.AppendLine($"OccurredAt: {occurredAt:O}");
        builder.AppendLine($"Source: {source}");
        builder.AppendLine($"IsTerminating: {isTerminating}");
        builder.AppendLine($"ProcessPath: {Environment.ProcessPath ?? "unknown"}");
        builder.AppendLine($"CommandLine: {Environment.CommandLine}");
        builder.AppendLine($"AppDataPath: {Current.LMCPath}");
        builder.AppendLine($"Version: {Current.Version}");
        builder.AppendLine($"Build: {Current.BuildNumber}");
        builder.AppendLine($"VersionType: {Current.VersionType}");
        builder.AppendLine($"OS: {RuntimeInformation.OSDescription}");
        builder.AppendLine($"OSArchitecture: {RuntimeInformation.OSArchitecture}");
        builder.AppendLine($"ProcessArchitecture: {RuntimeInformation.ProcessArchitecture}");
        builder.AppendLine($"Framework: {RuntimeInformation.FrameworkDescription}");
        builder.AppendLine();
        builder.AppendLine("Exception");
        builder.AppendLine("---------");
        builder.AppendLine(exception.ToString());
        builder.AppendLine();
        builder.AppendLine("Latest Log");
        builder.AppendLine("----------");
        builder.AppendLine($"Path: {latestLogPath}");
        builder.AppendLine(ReadLatestLogTail(latestLogPath));
        return builder.ToString();
    }

    private static string ReadLatestLogTail(string latestLogPath)
    {
        try
        {
            if (!File.Exists(latestLogPath))
            {
                return "(latest.log not found)";
            }

            var lines = File.ReadAllLines(latestLogPath);
            if (lines.Length == 0)
            {
                return "(latest.log is empty)";
            }

            const int maxLines = 200;
            return string.Join(Environment.NewLine, lines.Skip(Math.Max(0, lines.Length - maxLines)));
        }
        catch (Exception ex)
        {
            return $"(failed to read latest.log: {ex.Message})";
        }
    }

    private static void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception exception)
        {
            TryWriteReport(exception, "AppDomain.CurrentDomain.UnhandledException", e.IsTerminating);
        }
        else
        {
            TryWriteReport(
                new InvalidOperationException($"Unhandled exception object: {e.ExceptionObject}"),
                "AppDomain.CurrentDomain.UnhandledException",
                e.IsTerminating);
        }
    }

    private static void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        TryWriteReport(e.Exception, "TaskScheduler.UnobservedTaskException");
        e.SetObserved();
    }

    private static bool TryRegisterException(Exception exception)
    {
        var key = RuntimeHelpers.GetHashCode(exception);
        return s_reportedExceptions.TryAdd(key, 0);
    }

    private static void TryLogReportCreated(string reportPath, string source)
    {
        try
        {
            new Logger("CrashReport").Error($"应用程序发生未处理异常，已生成崩溃报告: {reportPath} (source: {source})");
        }
        catch
        {
        }
    }
}
