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
using LMC.LifeCycle;

namespace LineLauncherCs.Tests;

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
