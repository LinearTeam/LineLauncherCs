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

using LMCCore.Game.Launching.Steps.Arguments;
using LMCCore.Game.Model.LocalVersion;
using LMCCore.Game.Model.LocalVersion.Libraries;

namespace LineLauncherCs.Tests;

public class ProcessLaunchArgumentsStepHandlerTests
{
    [Fact]
    public void BuildClassPaths_ExcludesOnlyLibrariesWithExplicitNativeMetadata()
    {
        var rootPath = @"E:\games\.minecraft";
        var versionJarPath = Path.Combine(rootPath, "versions", "1.21.1-test", "1.21.1-test.jar");
        var versionInfo = new LocalVersionInfo
        {
            Id = "1.21.1-test",
            MainClass = "net.minecraft.client.main.Main",
            Libraries =
            [
                new SimpleLibraryInfo
                {
                    Name = "com.example:alpha:1.0.0"
                },
                new SimpleLibraryInfo
                {
                    Name = "org.lwjgl:lwjgl-glfw:3.3.3:natives-windows"
                },
                new LibraryInfo
                {
                    Name = "com.example:beta:2.0.0",
                    Downloads = new LibraryDownloadInfo
                    {
                        Artifact = new DownloadableFileInfo
                        {
                            Path = "com/example/beta/2.0.0/beta-2.0.0.jar"
                        }
                    }
                },
                new LibraryInfo
                {
                    Name = "org.lwjgl:lwjgl-opengl:3.3.3",
                    Natives = new Dictionary<string, string>
                    {
                        ["windows"] = "natives-windows"
                    },
                    Downloads = new LibraryDownloadInfo
                    {
                        Artifact = new DownloadableFileInfo
                        {
                            Path = "org/lwjgl/lwjgl-opengl/3.3.3/lwjgl-opengl-3.3.3.jar"
                        },
                        Classifiers = new Dictionary<string, DownloadableFileInfo>
                        {
                            ["natives-windows"] = new()
                            {
                                Path = "org/lwjgl/lwjgl-opengl/3.3.3/lwjgl-opengl-3.3.3-natives-windows.jar"
                            }
                        }
                    }
                },
                new LibraryInfo
                {
                    Name = "com.example:natives-only:1.0.0-natives-windows",
                    Path = "com/example/natives-only/1.0.0-natives-windows/natives-only-1.0.0-natives-windows.jar"
                }
            ]
        };

        var classPaths = ProcessLaunchArgumentsStepHandler.BuildClassPaths(
            rootPath,
            versionJarPath,
            versionInfo,
            CancellationToken.None);

        Assert.Equal(
            [
                Path.Combine(rootPath, "libraries", "com", "example", "alpha", "1.0.0", "alpha-1.0.0.jar"),
                Path.Combine(rootPath, "libraries", "org", "lwjgl", "lwjgl-glfw", "3.3.3", "lwjgl-glfw-3.3.3-natives-windows.jar"),
                Path.Combine(rootPath, "libraries", "com", "example", "beta", "2.0.0", "beta-2.0.0.jar"),
                Path.Combine(rootPath, "libraries", "com", "example", "natives-only", "1.0.0-natives-windows", "natives-only-1.0.0-natives-windows.jar"),
                versionJarPath
            ],
            classPaths);
    }

    [Fact]
    public void BuildClassPaths_DeduplicatesLibrariesAndKeepsInsertionOrder()
    {
        var rootPath = @"E:\games\.minecraft";
        var versionJarPath = Path.Combine(rootPath, "versions", "dup-test", "dup-test.jar");
        var versionInfo = new LocalVersionInfo
        {
            Id = "dup-test",
            MainClass = "net.minecraft.client.main.Main",
            Libraries =
            [
                new SimpleLibraryInfo
                {
                    Name = "com.example:shared:1.0.0"
                },
                new LibraryInfo
                {
                    Name = "com.example:shared:1.0.0",
                    Path = "com/example/shared/1.0.0/shared-1.0.0.jar"
                }
            ]
        };

        var classPaths = ProcessLaunchArgumentsStepHandler.BuildClassPaths(
            rootPath,
            versionJarPath,
            versionInfo,
            CancellationToken.None);

        Assert.Equal(
            [
                Path.Combine(rootPath, "libraries", "com", "example", "shared", "1.0.0", "shared-1.0.0.jar"),
                versionJarPath
            ],
            classPaths);
    }

    [Fact]
    public void BuildClassPaths_WithMissingVersionInfo_ReturnsVersionJarOnly()
    {
        var rootPath = @"E:\games\.minecraft";
        var versionJarPath = Path.Combine(rootPath, "versions", "jar-only", "jar-only.jar");

        var classPaths = ProcessLaunchArgumentsStepHandler.BuildClassPaths(
            rootPath,
            versionJarPath,
            versionInfo: null,
            CancellationToken.None);

        Assert.Equal([versionJarPath], classPaths);
    }

    [Fact]
    public void BuildClassPaths_WhenOnlyVersionDiffers_KeepsHighestVersion()
    {
        var rootPath = @"E:\games\.minecraft";
        var versionJarPath = Path.Combine(rootPath, "versions", "highest-version", "highest-version.jar");
        var versionInfo = new LocalVersionInfo
        {
            Id = "highest-version",
            MainClass = "net.minecraft.client.main.Main",
            Libraries =
            [
                new SimpleLibraryInfo
                {
                    Name = "com.example:shared:1.2.0"
                },
                new SimpleLibraryInfo
                {
                    Name = "com.example:shared:1.10.0"
                },
                new SimpleLibraryInfo
                {
                    Name = "com.example:shared-helper:1.0.0"
                }
            ]
        };

        var classPaths = ProcessLaunchArgumentsStepHandler.BuildClassPaths(
            rootPath,
            versionJarPath,
            versionInfo,
            CancellationToken.None);

        Assert.Equal(
            [
                Path.Combine(rootPath, "libraries", "com", "example", "shared", "1.10.0", "shared-1.10.0.jar"),
                Path.Combine(rootPath, "libraries", "com", "example", "shared-helper", "1.0.0", "shared-helper-1.0.0.jar"),
                versionJarPath
            ],
            classPaths);
    }
}
