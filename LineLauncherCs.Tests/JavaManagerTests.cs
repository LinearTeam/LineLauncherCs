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
using System.Runtime.InteropServices;
using LMC;
using LMC.Basic.Configs;
using LMCCore.Java;
using LMCCore.Java.Discovery;

namespace LineLauncherCs.Tests;

public class JavaManagerTests
{
    [Fact]
    public void JavaPathNormalizer_NormalizesAndDeduplicatesRoots()
    {
        var root = Path.Combine("C:\\", "Java", "jdk");
        var paths = JavaPathNormalizer.DistinctNormalizedRoots([root, root + "\\"]);

        Assert.Single(paths);
        Assert.Equal(root, paths[0]);
    }

    [Fact]
    public void JavaInstallationInfoParser_ParsesReleaseFile()
    {
        var lines = new[]
        {
            "JAVA_VERSION=\"17.0.9\"",
            "IMPLEMENTOR=\"Eclipse Adoptium\""
        };

        var java = JavaInstallationInfoParser.Parse("C:\\Java\\jdk", lines, isWindows: true);

        Assert.Equal("C:\\Java\\jdk", java.Path);
        Assert.Equal(new Version(17, 0, 9), java.Version);
        Assert.Equal("Eclipse Adoptium", java.Implementor);
    }

    [Fact]
    public async Task JavaDirectoryScanner_SkipsMissingDirectoriesAndStopsAtDepthLimit()
    {
        using var scope = new TestFileSystemScope();
        var root = scope.CreateDirectory("root");
        Directory.CreateDirectory(Path.Combine(root, "a", "b", "c"));
        var found = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var scanner = new JavaDirectoryScanner(
            directory => Task.FromResult(directory.EndsWith(Path.Combine("a", "b", "c"), StringComparison.OrdinalIgnoreCase)),
            new LMC.Basic.Logging.Logger("JavaManagerTests"),
            maxDepth: 2);

        await scanner.ScanDirectoryRecursivelyAsync(scope.GetPath("missing"), found);
        await scanner.ScanDirectoryRecursivelyAsync(root, found);

        Assert.Empty(found);
    }

    [Fact]
    public async Task JavaManager_AddJavasAsync_DeduplicatesAndCachesInfo()
    {
        using var scope = new TestFileSystemScope();
        var configDir = scope.CreateDirectory("config");
        var validRoot = CreateJavaRoot(scope, "jdk-17");
        var invalidRoot = scope.CreateDirectory("invalid");

        ConfigManager.ConfigDirectoryOverride = configDir;
        Current.Config = new AppConfig();
        JavaManager.ResetForTesting();

        try
        {
            await JavaManager.AddJavasAsync([validRoot, validRoot + Path.DirectorySeparatorChar, invalidRoot]);

            Assert.Single(Current.Config.JavaPaths);
            Assert.Equal(JavaPathNormalizer.NormalizeRootPath(validRoot), Current.Config.JavaPaths[0]);

            var first = await JavaManager.GetJavaInfo(validRoot);
            var second = await JavaManager.GetJavaInfo(validRoot);
            Assert.Same(first, second);
        }
        finally
        {
            ConfigManager.ConfigDirectoryOverride = null;
            JavaManager.ResetForTesting();
        }
    }

    private static string CreateJavaRoot(TestFileSystemScope scope, string relativePath)
    {
        var root = scope.CreateDirectory(relativePath);
        var binDir = Directory.CreateDirectory(Path.Combine(root, "bin")).FullName;
        File.WriteAllText(Path.Combine(binDir, RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "java.exe" : "java"), string.Empty);
        File.WriteAllText(Path.Combine(binDir, RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "javac.exe" : "javac"), string.Empty);
        File.WriteAllLines(Path.Combine(root, "release"),
        [
            "JAVA_VERSION=\"17.0.9\"",
            "IMPLEMENTOR=\"Eclipse Adoptium\""
        ]);
        return root;
    }
}
