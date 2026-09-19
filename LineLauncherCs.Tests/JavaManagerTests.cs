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
using LMCCore.Game.Model;
using LMCCore.Game.Model.LocalVersion;
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
    public void JavaInstallationInfoParser_NormalizesLegacyJavaVersion()
    {
        var java = JavaInstallationInfoParser.Parse(
            "C:\\Java\\jre8",
            ["JAVA_VERSION=\"1.8.0_401\"", "OS_ARCH=\"amd64\""],
            isWindows: true);

        Assert.Equal(new Version(8, 0, 401), java.Version);
        Assert.True(java.Is64Bit);
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

    [Fact]
    public async Task JavaManager_TryGetJavaInfo_ReturnsNullForAnInvalidRoot()
    {
        using var scope = new TestFileSystemScope();

        var java = await JavaManager.TryGetJavaInfo(scope.CreateDirectory("invalid"));

        Assert.Null(java);
    }

    [Fact]
    public async Task JavaManager_SelectJavaAsync_UsesVersionCompatibleRuntime()
    {
        using var scope = new TestFileSystemScope();
        var java8 = CreateJavaRoot(scope, "java-8", "1.8.0_401");
        var java17 = CreateJavaRoot(scope, "java-17", "17.0.12");
        var java21 = CreateJavaRoot(scope, "java-21", "21.0.4");
        var gameVersion = CreateGameVersion("1.20.4");

        JavaManager.ResetForTesting();
        try
        {
            var selected = await JavaManager.SelectJavaAsync(
                [java8, java21, java17],
                null,
                autoSelectJava: true,
                gameVersion);

            Assert.Equal(17, selected.Version.Major);
        }
        finally
        {
            JavaManager.ResetForTesting();
        }
    }

    [Fact]
    public void JavaCompatibilityPolicy_UsesDeclaredJavaVersion()
    {
        var gameVersion = CreateGameVersion("1.21.5", requiredJavaMajor: 21);

        var requirement = JavaCompatibilityPolicy.Resolve(gameVersion);

        Assert.Equal(21, requirement.PreferredMajor);
        Assert.Equal(21, requirement.MinimumMajor);
        Assert.Null(requirement.MaximumMajor);
    }

    private static string CreateJavaRoot(
        TestFileSystemScope scope,
        string relativePath,
        string version = "17.0.9")
    {
        var root = scope.CreateDirectory(relativePath);
        var binDir = Directory.CreateDirectory(Path.Combine(root, "bin")).FullName;
        File.WriteAllText(Path.Combine(binDir, RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "java.exe" : "java"), string.Empty);
        File.WriteAllText(Path.Combine(binDir, RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "javac.exe" : "javac"), string.Empty);
        File.WriteAllLines(Path.Combine(root, "release"),
        [
            $"JAVA_VERSION=\"{version}\"",
            "IMPLEMENTOR=\"Eclipse Adoptium\"",
            "OS_ARCH=\"amd64\""
        ]);
        return root;
    }

    private static LocalGameVersionEntry CreateGameVersion(string clientVersion, int? requiredJavaMajor = null)
    {
        return new LocalGameVersionEntry
        {
            RootPath = "game",
            VersionName = clientVersion,
            VersionDirectory = Path.Combine("game", "versions", clientVersion),
            ClientVersionId = clientVersion,
            Status = VersionStatus.Valid,
            VersionInfo = new LocalVersionInfo
            {
                Id = clientVersion,
                MainClass = "net.minecraft.client.main.Main",
                Libraries = [],
                RequiredJavaVersion = requiredJavaMajor is { } major
                    ? new GameJavaVersionInfo { MajorVersion = major }
                    : null
            }
        };
    }
}
