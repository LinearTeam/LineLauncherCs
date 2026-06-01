using System.Runtime.InteropServices;
using LMC.Basic.Configs;
using LMCCore.Game.Download;
using LMCCore.Game.Download.Model.Vanilla;
using LMCCore.Game.Download.Vanilla;
using LMCCore.Game.Model;
using LMCCore.Game.Model.LocalVersion;
using LMCCore.Game.Model.LocalVersion.Arguments;
using LMCCore.Game.Model.LocalVersion.Compatibility;
using LMCCore.Game.Model.LocalVersion.Libraries;
using LMCCore.Game.Versioning;
using LMC.Basic.Logging;

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
}
