using LMC.LifeCycle;

namespace LMC.Tests;

public class CrashReportManagerTests
{
    [Fact]
    public void WriteReport_CreatesCrashFileWithMetadataAndLogTail()
    {
        using var scope = new TestFileSystemScope();
        var appDataPath = scope.CreateDirectory("appdata");
        var logsPath = scope.CreateDirectory(Path.Combine("appdata", "logs"));
        var latestLogPath = Path.Combine(logsPath, "latest.log");
        File.WriteAllLines(latestLogPath,
        [
            "line-1",
            "line-2",
            "line-3"
        ]);

        var reportPath = CrashReportManager.WriteReport(
            new InvalidOperationException("boom"),
            "UnitTest",
            appDataPath,
            latestLogPath,
            isTerminating: true,
            timestamp: new DateTimeOffset(2026, 6, 2, 1, 30, 0, TimeSpan.Zero));

        var content = File.ReadAllText(reportPath);

        Assert.StartsWith(Path.Combine(appDataPath, "crashes"), reportPath, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("LineLauncherCs Crash Report", content);
        Assert.Contains("Source: UnitTest", content);
        Assert.Contains("IsTerminating: True", content);
        Assert.Contains("InvalidOperationException: boom", content);
        Assert.Contains($"Path: {latestLogPath}", content);
        Assert.Contains("line-3", content);
    }
}
