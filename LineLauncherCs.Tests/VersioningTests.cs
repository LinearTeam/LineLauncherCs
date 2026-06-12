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
using System.Text.Json.Nodes;
using LMC.Basic.Configs;
using LMCCore.Game.Download;
using LMCCore.Game.Download.Model.Vanilla;
using LMCCore.Game.Download.Vanilla;
using LMCCore.Game.Model;
using LMCCore.Game.Model.Configuration;
using LMCCore.Game.Model.LocalVersion;
using LMCCore.Game.Model.LocalVersion.Arguments;
using LMCCore.Game.Model.LocalVersion.Compatibility;
using LMCCore.Game.Model.LocalVersion.Libraries;
using LMCCore.Game.Versioning;
using LMCCore.Game.Versioning.Discovery;
using LMC.Basic.Logging;
using LMCCore.Game.Model.Loaders;

namespace LineLauncherCs.Tests;

public class VersioningTests
{
    [Fact]
    public async Task LocalVersionScanner_FallsBackWhenVersionJsonContainsUnsupportedArgumentValue()
    {
        using var scope = new TestFileSystemScope();
        var rootPath = scope.CreateDirectory("gameRoot");
        var versionsPath = scope.CreateDirectory(Path.Combine("gameRoot", "versions"));
        var invalidShapeDir = Path.Combine(versionsPath, "invalidShape");
        Directory.CreateDirectory(invalidShapeDir);
        File.WriteAllText(Path.Combine(invalidShapeDir, "invalidShape.jar"), string.Empty);
        File.WriteAllText(Path.Combine(invalidShapeDir, "invalidShape.json"), """
        {
          "id": "invalidShape",
          "mainClass": "main",
          "libraries": [],
          "arguments": {
            "game": [
              {
                "value": {
                  "unexpected": true
                }
              }
            ]
          }
        }
        """);

        var scanner = new LocalVersionScanner(
            new DownloadManager(),
            new VersionClientVersionResolver(_ => throw new InvalidOperationException("manifest loader should not be called"), new Logger("VersioningTests")),
            new Logger("VersioningTests"));

        var versions = await scanner.ScanVersionsAsync(rootPath);
        var entry = Assert.Single(versions);

        Assert.Equal("invalidShape", entry.VersionName);
        Assert.Equal(VersionStatus.InvalidJson, entry.Status);
        Assert.Null(entry.VersionInfo);
    }

    [Fact]
    public async Task ClientVersionResolver_UsesSourcesInPriorityOrder()
    {
        var releaseTime = DateTimeOffset.Parse("2024-01-01T00:00:00Z");
        var resolver = new VersionClientVersionResolver(
            _ => Task.FromResult(new VersionManifestInfo
            {
                Versions =
                [
                    new VersionEntry
                    {
                        Id = "1.20.4",
                        Type = "release",
                        Url = "https://example.com",
                        Time = releaseTime,
                        ReleaseTime = releaseTime
                    }
                ]
            }),
            new Logger("VersioningTests"));

        var clientVersion = await resolver.ResolveAsync(CreateVersionInfo(clientVersion: "client"));
        var patchVersion = await resolver.ResolveAsync(CreateVersionInfo(patchVersion: "patch"));
        var argumentVersion = await resolver.ResolveAsync(CreateVersionInfo(gameArgumentVersion: "argument"));
        var libraryVersion = await resolver.ResolveAsync(CreateVersionInfo(libraryName: "net.minecraftforge:forge:1.18.2-40.0.0"));
        var manifestVersion = await resolver.ResolveAsync(CreateVersionInfo(releaseTime: releaseTime));

        Assert.Equal("client", clientVersion);
        Assert.Equal("patch", patchVersion);
        Assert.Equal("argument", argumentVersion);
        Assert.Equal("1.18.2", libraryVersion);
        Assert.Equal("1.20.4", manifestVersion);
    }

    [Fact]
    public void ManagedGameRootService_AddRemoveAndSelectRoots()
    {
        using var scope = new TestFileSystemScope();
        var config = new AppConfig();
        var saves = 0;
        var service = new ManagedGameRootService(config, () => saves++);
        var rootA = scope.CreateDirectory("rootA");
        var rootB = scope.CreateDirectory("rootB");

        service.AddManagedRoot(rootA);
        service.AddManagedRoot(rootA);
        service.SetSelectedRoot(rootB);
        var removed = service.RemoveManagedRoot(rootB);

        Assert.Single(service.GetManagedRoots());
        Assert.True(removed);
        Assert.Equal(Path.GetFullPath(rootA).TrimEnd(Path.DirectorySeparatorChar), service.GetSelectedRoot()!.RootPath);
        Assert.Equal(3, saves);
    }

    [Fact]
    public async Task LocalVersionScanner_SetsMissingAndInvalidStatuses()
    {
        using var scope = new TestFileSystemScope();
        var rootPath = scope.CreateDirectory("gameRoot");
        var versionsPath = scope.CreateDirectory(Path.Combine("gameRoot", "versions"));

        var jarOnlyDir = Path.Combine(versionsPath, "jarOnly");
        Directory.CreateDirectory(jarOnlyDir);
        File.WriteAllText(Path.Combine(jarOnlyDir, "jarOnly.jar"), string.Empty);

        var jsonOnlyDir = Path.Combine(versionsPath, "jsonOnly");
        Directory.CreateDirectory(jsonOnlyDir);
        File.WriteAllText(Path.Combine(jsonOnlyDir, "jsonOnly.json"), """
        {
          "id": "jsonOnly",
          "mainClass": "main",
          "libraries": []
        }
        """);

        var invalidJsonDir = Path.Combine(versionsPath, "invalidJson");
        Directory.CreateDirectory(invalidJsonDir);
        File.WriteAllText(Path.Combine(invalidJsonDir, "invalidJson.jar"), string.Empty);
        File.WriteAllText(Path.Combine(invalidJsonDir, "invalidJson.json"), "{bad json");

        var scanner = new LocalVersionScanner(
            new DownloadManager(),
            new VersionClientVersionResolver(_ => throw new InvalidOperationException("manifest loader should not be called"), new Logger("VersioningTests")),
            new Logger("VersioningTests"));

        var versions = await scanner.ScanVersionsAsync(rootPath);

        Assert.Equal(3, versions.Count);
        Assert.Equal(VersionStatus.MissingJson, versions.Single(version => version.VersionName == "jarOnly").Status);
        Assert.Equal(VersionStatus.MissingJar, versions.Single(version => version.VersionName == "jsonOnly").Status);
        Assert.Equal(VersionStatus.InvalidJson, versions.Single(version => version.VersionName == "invalidJson").Status);
    }

    [Fact]
    public void CompatibilityRuleEvaluator_RecognizesMatchingAndNonMatchingRules()
    {
        var matchingRule = new CompatibilityRule
        {
            Action = "allow",
            Os = new RuleOs
            {
                Name = PlatformDetector.GetCurrentOs(),
                Arch = RuntimeInformation.OSArchitecture switch
                {
                    Architecture.X64 => "x86_64",
                    Architecture.Arm => "arm32",
                    _ => RuntimeInformation.OSArchitecture.ToString().ToLowerInvariant()
                }
            }
        };

        var nonMatchingRule = new CompatibilityRule
        {
            Action = "allow",
            Os = new RuleOs
            {
                Name = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "linux" : "windows"
            }
        };

        Assert.True(CompatibilityRuleEvaluator.RuleMatchesOs(matchingRule));
        Assert.False(CompatibilityRuleEvaluator.RuleMatchesOs(nonMatchingRule));
        Assert.True(CompatibilityRuleEvaluator.CheckRulesApply([matchingRule]));
        Assert.False(CompatibilityRuleEvaluator.CheckRulesApply([nonMatchingRule]));
    }

    [Fact]
    public void VersionConfigManager_WriteIndependentConfig_WritesToVersionJson()
    {
        using var scope = new TestFileSystemScope();
        var version = CreateLocalGameVersionEntry(scope, "jsonConfig");
        var manager = new VersionConfigManager();

        manager.WriteIndependentConfig(
            version,
            JsonNode.Parse("""{"java":{"maxMemoryMb":4096}}""")!,
            VersionConfigSourceType.VersionJson);

        var rootObject = JsonNode.Parse(File.ReadAllText(version.JsonPath!))!.AsObject();
        Assert.NotNull(rootObject["LMCConfig"]);
        Assert.Equal(4096, rootObject["LMCConfig"]!["java"]!["maxMemoryMb"]!.GetValue<int>());
    }

    [Fact]
    public void VersionConfigManager_WriteIndependentConfigModel_PersistsAndReadsUnifiedModel()
    {
        using var scope = new TestFileSystemScope();
        var version = CreateLocalGameVersionEntry(scope, "modelConfig");
        var manager = new VersionConfigManager();
        var config = new GameVersionConfig
        {
            Java = new GameVersionJavaConfig
            {
                MaxMemoryMb = 6144
            },
            Game = new GameVersionGameConfig
            {
                VersionId = "1.20.1",
                ModLoaders = [new ModLoader
                {
                    Type = ModLoaderType.Fabric, VersionId = "0.18.0"
                }]
            },
            Launcher = new GameVersionLauncherConfig
            {
                ShowLog = true
            },
            IconPath = @"E:\icons\grass.png"
        };

        manager.WriteIndependentConfig(version, config, VersionConfigSourceType.VersionJson);

        var loaded = manager.GetIndependentConfigModel(version);

        Assert.NotNull(loaded);
        Assert.Equal(6144, loaded!.Java!.MaxMemoryMb);
        Assert.Equal("1.20.1", loaded.Game!.VersionId);
        Assert.Equal("0.18.0", loaded.Game!.ModLoaders![0].VersionId);
        Assert.Equal(ModLoaderType.Fabric, loaded.Game!.ModLoaders![0].Type);
        Assert.True(loaded.Launcher!.ShowLog);
        Assert.Equal(@"E:\icons\grass.png", loaded.IconPath);
    }

    [Fact]
    public void VersionConfigManager_WriteIndependentConfig_WritesToVersionFolder()
    {
        using var scope = new TestFileSystemScope();
        var version = CreateLocalGameVersionEntry(scope, "folderConfig");
        var manager = new VersionConfigManager();

        manager.WriteIndependentConfig(
            version,
            JsonNode.Parse("""{"game":{"fullscreen":true}}""")!,
            NewVersionConfigSource.VersionFolder);

        var configPath = Path.Combine(version.VersionDirectory, "LMC", "version_config.json");
        var rootObject = JsonNode.Parse(File.ReadAllText(configPath))!.AsObject();
        Assert.True(rootObject["game"]!["fullscreen"]!.GetValue<bool>());
    }

    [Fact]
    public void VersionConfigManager_WriteIndependentConfig_WritesToLmcDataDirectory()
    {
        using var scope = new TestFileSystemScope();
        var version = CreateLocalGameVersionEntry(scope, "dataConfig");
        var versionConfigsPath = scope.GetPath("version_configs.json");
        var manager = new VersionConfigManager(versionConfigsPath: versionConfigsPath);

        manager.WriteIndependentConfig(
            version,
            JsonNode.Parse("""{"launcher":{"showLog":true}}""")!,
            VersionConfigSourceType.LMCDataDirectory);

        var rootObject = JsonNode.Parse(File.ReadAllText(versionConfigsPath))!.AsObject();
        var normalizedVersionDirectory = Path.GetFullPath(version.VersionDirectory)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        Assert.True(rootObject[normalizedVersionDirectory]!["launcher"]!["showLog"]!.GetValue<bool>());
    }

    private static LocalVersionInfo CreateVersionInfo(
        string? clientVersion = null,
        string? patchVersion = null,
        string? gameArgumentVersion = null,
        string? libraryName = null,
        DateTimeOffset? releaseTime = null)
    {
        var libraries = new List<ILibraryInfo>
        {
            new LibraryInfo
            {
                Name = libraryName ?? "com.example:test:1.0.0"
            }
        };

        var arguments = gameArgumentVersion == null
            ? null
            : new Dictionary<string, List<IGameArgument>>
            {
                ["game"] =
                [
                    new StringGameArgument { Value = "--fml.mcVersion" },
                    new StringGameArgument { Value = gameArgumentVersion }
                ]
            };

        return new LocalVersionInfo
        {
            Id = "test-version",
            MainClass = "main",
            Libraries = libraries,
            ClientVersion = clientVersion,
            Patches = patchVersion == null
                ? null
                :
                [
                    new HMCLPatchInfo { Id = "game", Version = patchVersion }
                ],
            Arguments = arguments,
            ReleaseTime = releaseTime
        };
    }

    private static LocalGameVersionEntry CreateLocalGameVersionEntry(TestFileSystemScope scope, string versionName)
    {
        var rootPath = scope.CreateDirectory("gameRoot");
        var versionDirectory = scope.CreateDirectory(Path.Combine("gameRoot", "versions", versionName));
        var jsonPath = Path.Combine(versionDirectory, $"{versionName}.json");
        var jarPath = Path.Combine(versionDirectory, $"{versionName}.jar");

        File.WriteAllText(jsonPath, $$"""
        {
          "id": "{{versionName}}",
          "mainClass": "main",
          "libraries": []
        }
        """);
        File.WriteAllText(jarPath, string.Empty);

        return new LocalGameVersionEntry
        {
            RootPath = rootPath,
            VersionName = versionName,
            VersionDirectory = versionDirectory,
            JsonPath = jsonPath,
            JarPath = jarPath,
            Status = VersionStatus.Valid
        };
    }
}
