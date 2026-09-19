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
using System.Text.Json;
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
using LMCCore.Utils;
using LMC.Basic.Logging;
using LMCCore.Game.Model.Loaders;

namespace LineLauncherCs.Tests;

public class VersioningTests
{
    [Theory]
    [InlineData("")]
    [InlineData(".")]
    [InlineData("..")]
    [InlineData("invalid/name")]
    public void VersionNameValidator_RejectsInvalidPathSegments(string versionName)
    {
        Assert.False(VersionNameValidator.IsValid(versionName));
    }

    [Fact]
    public void VersionNameValidator_RejectsWindowsDeviceNames()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        Assert.False(VersionNameValidator.IsValid("CON"));
        Assert.False(VersionNameValidator.IsValid("COM1.json"));
        Assert.False(VersionNameValidator.IsValid("version."));
        Assert.Throws<ArgumentException>(() => LocalVersionRenamer.ValidateVersionName("LPT9"));
    }

    [Fact]
    public void LocalVersionRenamer_RenamesOwnedFilesAndUpdatesReferences()
    {
        using var scope = new TestFileSystemScope();
        var rootPath = scope.CreateDirectory("gameRoot");
        var versionsPath = scope.CreateDirectory(Path.Combine("gameRoot", "versions"));
        var oldDirectory = scope.CreateDirectory(Path.Combine("gameRoot", "versions", "old"));
        var childDirectory = scope.CreateDirectory(Path.Combine("gameRoot", "versions", "child"));
        var oldJsonPath = Path.Combine(oldDirectory, "old.json");
        File.WriteAllText(oldJsonPath, """
        { "id": "old", "mainClass": "main", "libraries": [] }
        """);
        File.WriteAllText(Path.Combine(oldDirectory, "old.jar"), "jar");
        Directory.CreateDirectory(Path.Combine(oldDirectory, "old-natives"));
        var childJsonPath = Path.Combine(childDirectory, "child.json");
        File.WriteAllText(childJsonPath, """
        { "id": "child", "inheritsFrom": "old", "mainClass": "main", "libraries": [] }
        """);
        var configPath = scope.GetPath("version_configs.json");
        File.WriteAllText(configPath, new JsonObject
        {
            [Path.GetFullPath(oldDirectory)] = new JsonObject { ["iconPath"] = "icon.png" }
        }.ToJsonString());
        var version = new LocalGameVersionEntry
        {
            RootPath = rootPath,
            VersionName = "old",
            VersionDirectory = oldDirectory,
            JsonPath = oldJsonPath,
            JarPath = Path.Combine(oldDirectory, "old.jar"),
            Status = VersionStatus.Valid
        };

        new LocalVersionRenamer(configPath).Rename(version, "new");

        var newDirectory = Path.Combine(versionsPath, "new");
        Assert.False(Directory.Exists(oldDirectory));
        Assert.True(File.Exists(Path.Combine(newDirectory, "new.json")));
        Assert.True(File.Exists(Path.Combine(newDirectory, "new.jar")));
        Assert.True(Directory.Exists(Path.Combine(newDirectory, "new-natives")));
        Assert.Equal("new", JsonNode.Parse(File.ReadAllText(Path.Combine(newDirectory, "new.json")))?["id"]?.GetValue<string>());
        Assert.Equal("new", JsonNode.Parse(File.ReadAllText(childJsonPath))?["inheritsFrom"]?.GetValue<string>());
        var config = JsonNode.Parse(File.ReadAllText(configPath))!.AsObject();
        Assert.False(config.ContainsKey(Path.GetFullPath(oldDirectory)));
        Assert.True(config.ContainsKey(Path.GetFullPath(newDirectory)));
    }

    [Fact]
    public async Task VersionManager_RefreshVersionsAsync_ReplacesCachedCatalogAfterInstallation()
    {
        using var scope = new TestFileSystemScope();
        var rootPath = scope.CreateDirectory("gameRoot");
        var versionsPath = scope.CreateDirectory(Path.Combine("gameRoot", "versions"));
        var existingVersionPath = Path.Combine(versionsPath, "existing");
        Directory.CreateDirectory(existingVersionPath);
        File.WriteAllText(Path.Combine(existingVersionPath, "existing.jar"), string.Empty);

        var versionManager = new VersionManager();
        Assert.Single(await versionManager.ScanVersionsAsync(rootPath));

        var installedVersionPath = Path.Combine(versionsPath, "installed");
        Directory.CreateDirectory(installedVersionPath);
        File.WriteAllText(Path.Combine(installedVersionPath, "installed.jar"), string.Empty);

        Assert.Single(await versionManager.GetCachedOrScanVersionsAsync(rootPath));

        var refreshedVersions = await versionManager.RefreshVersionsAsync(rootPath);

        Assert.Equal(2, refreshedVersions.Count);
        Assert.Contains(refreshedVersions, version => version.VersionName == "installed");
        Assert.Equal(2, (await versionManager.GetCachedOrScanVersionsAsync(rootPath)).Count);
    }

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
    public async Task LocalVersionScanner_CanReadLaunchVersionWithoutFetchingManifest()
    {
        using var scope = new TestFileSystemScope();
        var rootPath = scope.CreateDirectory("gameRoot");
        var versionDirectory = scope.CreateDirectory(Path.Combine("gameRoot", "versions", "selected"));
        File.WriteAllText(Path.Combine(versionDirectory, "selected.jar"), string.Empty);
        File.WriteAllText(Path.Combine(versionDirectory, "selected.json"), """
        {
          "id": "selected",
          "mainClass": "main",
          "libraries": [],
          "releaseTime": "2024-01-01T00:00:00Z"
        }
        """);

        var scanner = new LocalVersionScanner(
            new DownloadManager(),
            new VersionClientVersionResolver(
                _ => throw new InvalidOperationException("manifest loader should not be called"),
                new Logger("VersioningTests")),
            new Logger("VersioningTests"));

        var entry = await scanner.CreateVersionEntryAsync(
            rootPath,
            versionDirectory,
            CancellationToken.None,
            resolveClientVersion: false);

        Assert.NotNull(entry);
        Assert.Equal(VersionStatus.Valid, entry.Status);
        Assert.Equal(LocalGameVersionEntry.UnknownClientVersionId, entry.ClientVersionId);
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
    public void PlatformDetector_UsesOnlyCommonPlatformTokens()
    {
        Assert.Equal(
            Environment.Is64BitProcess ? "64" : "32",
            PlatformDetector.GetNativeArchitectureToken());
        Assert.Equal(OSPlatform.Windows, PlatformDetector.ParseOsName("windows"));
        Assert.Equal(OSPlatform.OSX, PlatformDetector.ParseOsName("osx"));
        Assert.Equal(OSPlatform.Linux, PlatformDetector.ParseOsName("linux"));
        Assert.Null(PlatformDetector.ParseOsName("freebsd"));
        Assert.Null(PlatformDetector.ParseOsName("solaris"));

        Assert.Equal(Architecture.X86, PlatformDetector.ParseArchName("x86"));
        Assert.Equal(Architecture.X86, PlatformDetector.ParseArchName("custom-x86"));
        Assert.Equal(Architecture.X64, PlatformDetector.ParseArchName("x64"));
        Assert.Equal(Architecture.X64, PlatformDetector.ParseArchName("custom-x64"));
        Assert.Equal(Architecture.Arm, PlatformDetector.ParseArchName("armv7"));
        Assert.Equal(Architecture.Arm64, PlatformDetector.ParseArchName("arm64-v8a"));
        Assert.Null(PlatformDetector.ParseArchName("i686"));
        Assert.Null(PlatformDetector.ParseArchName("amd64"));
        Assert.Null(PlatformDetector.ParseArchName("ppc64"));
        Assert.Null(PlatformDetector.ParseArchName("loongarch64"));
    }

    [Fact]
    public void CompatibilityRuleEvaluator_UsesPlatformRulesBeforeDefaultRules()
    {
        var currentOs = PlatformDetector.GetCurrentOs();
        var otherOs = currentOs == "windows" ? "linux" : "windows";

        Assert.True(CompatibilityRuleEvaluator.CheckRulesApply([
            new CompatibilityRule { Action = "allow", Os = new RuleOs { Name = currentOs } }
        ]));
        Assert.False(CompatibilityRuleEvaluator.CheckRulesApply([
            new CompatibilityRule { Action = "allow", Os = new RuleOs { Name = otherOs } }
        ]));
        Assert.True(CompatibilityRuleEvaluator.CheckRulesApply([
            new CompatibilityRule { Action = "disallow", Os = new RuleOs { Name = otherOs } }
        ]));
        Assert.False(CompatibilityRuleEvaluator.CheckRulesApply([
            new CompatibilityRule { Action = "disallow", Os = new RuleOs { Name = currentOs } }
        ]));
        Assert.True(CompatibilityRuleEvaluator.CheckRulesApply([
            new CompatibilityRule { Action = "allow" }
        ]));
        Assert.False(CompatibilityRuleEvaluator.CheckRulesApply([
            new CompatibilityRule { Action = "allow", Os = new RuleOs { Name = otherOs } },
            new CompatibilityRule { Action = "disallow" }
        ]));
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
    public void VersionConfigManager_GetEffectiveConfig_PrefersVersionValuesOverGlobalValues()
    {
        using var scope = new TestFileSystemScope();
        var version = CreateLocalGameVersionEntry(scope, "effectiveConfig");
        var globalConfigPath = scope.GetPath("global_version_config.json");
        File.WriteAllText(
            globalConfigPath,
            JsonSerializer.Serialize(
                new GameVersionConfig
                {
                    Java = new GameVersionJavaConfig
                    {
                        MaxMemoryMb = 4096,
                        JvmArguments = "-Dglobal=true"
                    }
                },
                JsonUtils.DefaultSerializerOptions));
        var manager = new VersionConfigManager(globalConfigPath: globalConfigPath);

        manager.WriteIndependentConfig(
            version,
            new GameVersionConfig
            {
                Java = new GameVersionJavaConfig
                {
                    MaxMemoryMb = 6144
                }
            },
            VersionConfigSourceType.VersionJson);

        var effectiveConfig = manager.GetEffectiveConfigModel(version);

        Assert.NotNull(effectiveConfig);
        Assert.Equal(6144, effectiveConfig!.Java!.MaxMemoryMb);
        Assert.Equal("-Dglobal=true", effectiveConfig.Java.JvmArguments);
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
