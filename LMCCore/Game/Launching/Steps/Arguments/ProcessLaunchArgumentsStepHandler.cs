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
using LMCCore.Account;
using LMCCore.Account.Model;
using LMCCore.Game.Download.Installation.Forge;
using LMCCore.Game.Download.Vanilla;
using LMCCore.Game.Launching.Execution;
using LMCCore.Game.Model.LocalVersion;
using LMCCore.Game.Model.LocalVersion.Arguments;
using LMCCore.Game.Model.LocalVersion.Compatibility;
using LMCCore.Game.Model.LocalVersion.Libraries;

namespace LMCCore.Game.Launching.Steps.Arguments;

public sealed class ProcessLaunchArgumentsStepHandler : IGameLaunchStepHandler
{
    public GameLaunchProgressStep Step => GameLaunchProgressStep.ProcessLaunchArguments;
    private readonly List<IGameArgument> _defaultJvmArguments = [
        new ConditionGameArguments
        {
            Value = new StringConditionArgumentValue{ Value = "-XX:HeapDumpPath=MojangTricksIntelDriversForPerformance_javaw.exe_minecraft.exe.heapdump" },
            Rules = [
                new CompatibilityRule
                {
                    Os = new RuleOs
                    {
                        Name = "windows"
                    },
                    Action = "allow"
                }
            ]
        },
        new StringGameArgument { Value = "-Djava.library.path=${natives_directory}" },
        new StringGameArgument { Value = "-Dminecraft.launcher.brand=${launcher_name}" },
        new StringGameArgument { Value = "-Dminecraft.launcher.version=${launcher_version}" },
        new StringGameArgument { Value = "-cp" },
        new StringGameArgument { Value = "${classpath}" }
    ];

    public Task ExecuteAsync(GameLaunchContext context, CancellationToken cancellationToken)
    {
        var cp = BuildClassPaths(context, cancellationToken);
        var cb = new CommandBuilder();
        var cpStr = string.Join(Path.PathSeparator, cp);
        cpStr = $"\"{cpStr}\"";
        
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) && OperatingSystem.IsWindowsVersionAtLeast(10))
        {
            cb.AddJvmArguments(["-Dos.name=Windows 10", "-Dos.version=10.0"]);
        }
        
        AddJvmArguments(context, cb, cpStr);
        AddGameArguments(context, cb, cpStr);

        cb.AddJvmArguments(["-Xmx4G"]); // TODO: 读配置

        AddUserDefaultJvmArguments(context, cb, cpStr);

        context.CommandBuilder = cb;
        return Task.CompletedTask;
    }

    private static string ReplaceVars(string s, GameLaunchContext context, string classPath)
    {
        s = s.Replace("${natives_directory}", Path.Combine(context.Version.VersionDirectory, $"natives-{RuntimeInformation.RuntimeIdentifier}"));
        s = s.Replace("${auth_player_name}", context.Account.Name);
        s = s.Replace("${auth_session}", context.Account.Type != AccountType.Microsoft 
            ? Guid.NewGuid().ToString("N")
            : ((MicrosoftAccount) context.Account).AccessToken);
        s = s.Replace("${auth_access_token}", context.Account.Type != AccountType.Microsoft 
            ? Guid.NewGuid().ToString("N")
            : ((MicrosoftAccount) context.Account).AccessToken);
        s = s.Replace("${auth_uuid}", context.Account.Uuid.Replace("-", "").ToLower());
        s = s.Replace("${user_type}", context.Account.Type == AccountType.Microsoft ? "msa" : "legacy");
        s = s.Replace("${user_properties}", "{}");
        s = s.Replace("${version_name}", context.Version.VersionName);
        s = s.Replace("${profile_name}", $"LMC {Current.Version}");
        s = s.Replace("${version_type}", $"LMC-{Current.VersionType}");
        s = s.Replace("${game_directory}", context.Version.VersionDirectory);
        s = s.Replace("${assets_index_name}", context.Version.VersionInfo.AssetIndex!.Id);
        // s = s.Replace("${resolution_width}", context.Account.Name);
        // s = s.Replace("${resolution_height}", context.Account.Name);
        // TODO: 设置窗口大小
        s = s.Replace("${library_directory}", Path.Combine(context.Version.RootPath, "libraries"));
        s = s.Replace("${libraries_directory}", Path.Combine(context.Version.RootPath, "libraries"));
        s = s.Replace("${classpath_separator}", Path.PathSeparator.ToString());
        s = s.Replace("${primary_jar}", context.Version.JarPath);
        s = s.Replace("${primary_jar_name}", Path.GetFileName(context.Version.JarPath));
        s = s.Replace("${language}", context.Config.SelectedLanguage.ToLower()); // TODO: 特殊判断?
        s = s.Replace("${launcher_name}", "LMC");
        s = s.Replace("${launcher_version}", Current.Version);
        s = s.Replace("${classpath}", classPath);
        s = s.Replace("${assets_root}", Path.Combine(context.Version.RootPath, "assets"));
        return s;
    }
    
    private void AddGameArguments(GameLaunchContext context, CommandBuilder cb, string classPath)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(context.Version.VersionInfo);
        var args = context.Version.VersionInfo.Arguments?.GetValueOrDefault("game");
        if (args == null)
        {
            var mcArgs = context.Version.VersionInfo.MinecraftArguments;
            if(string.IsNullOrEmpty(mcArgs)) return;
            args = new List<IGameArgument>(mcArgs.Split(' ')
                .Select(str => new StringGameArgument{ Value = str })
                .ToList());
        }
        var finalArgs = ProcessArgs(context, classPath, args);
        cb.AddGameArguments(finalArgs);
    }

    private void AddJvmArguments(GameLaunchContext context, CommandBuilder cb, string classPath)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(context.Version.VersionInfo);
        var args = context.Version.VersionInfo.Arguments?.GetValueOrDefault("jvm") ??  _defaultJvmArguments;
        var finalArgs = ProcessArgs(context, classPath, args);
        cb.AddJvmArguments(finalArgs);
    }
    private void AddUserDefaultJvmArguments(GameLaunchContext context, CommandBuilder cb, string classPath)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(context.Version.VersionInfo);
        if(context.Version.VersionInfo.Arguments == null) return;
        if (!context.Version.VersionInfo.Arguments.TryGetValue("default-user-jvm", out var args))
        {
            return;
        }
        var finalArgs = ProcessArgs(context, classPath, args);
        cb.AddJvmArguments(finalArgs);
    }
    
    private List<string> ProcessArgs(GameLaunchContext context, string classPath, List<IGameArgument> args)
    {

        var finalArgs = new List<string>();
        foreach (var s in args)
        {
            switch (s)
            {
                case StringGameArgument sga:
                    finalArgs.Add(ReplaceVars(sga.Value, context, classPath));
                    break;
                case ConditionGameArguments cga:
                    if(!CompatibilityRuleEvaluator.CheckRulesApply(cga.Rules)) continue;
                    if (cga.Rules != null && cga.Rules.Any(r => r.Features != null)) continue;
                    switch (cga.Value)
                    {
                        case StringConditionArgumentValue scav:
                            finalArgs.Add(ReplaceVars(scav.Value, context, classPath));
                            break;
                        case StringArrayConditionArgumentValue sacav:
                            finalArgs.AddRange(sacav.Values.Select(value => ReplaceVars(value, context, classPath)));
                            break;
                    }
                    break;
            }
        }
        return finalArgs;
    }

    private static IReadOnlyList<string> BuildClassPaths(GameLaunchContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        return BuildClassPaths(
            context.Version.RootPath,
            context.Version.JarPath,
            context.Version.VersionInfo,
            cancellationToken);
    }

    internal static IReadOnlyList<string> BuildClassPaths(
        string rootPath,
        string? versionJarPath,
        LocalVersionInfo? versionInfo,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootPath);

        if (versionInfo == null)
        {
            return string.IsNullOrWhiteSpace(versionJarPath)
                ? []
                : [versionJarPath];
        }

        var selectedCandidates = new Dictionary<string, ClassPathCandidate>(StringComparer.OrdinalIgnoreCase);
        var candidateOrder = new List<string>();

        foreach (var libInfo in versionInfo.Libraries)
        {
            cancellationToken.ThrowIfCancellationRequested();

            switch (libInfo)
            {
                case SimpleLibraryInfo simpleLibrary:
                    AddSimpleLibraryClassPath(rootPath, simpleLibrary, selectedCandidates, candidateOrder);
                    break;
                case LibraryInfo detailedLibrary:
                    AddDetailedLibraryClassPath(rootPath, detailedLibrary, selectedCandidates, candidateOrder);
                    break;
            }
        }

        var classPaths = candidateOrder
            .Select(key => selectedCandidates[key].Path)
            .ToList();
        var seenPaths = new HashSet<string>(classPaths, StringComparer.OrdinalIgnoreCase);

        if (!string.IsNullOrWhiteSpace(versionJarPath) && seenPaths.Add(versionJarPath))
        {
            classPaths.Add(versionJarPath);
        }

        return classPaths;
    }

    private static void AddSimpleLibraryClassPath(
        string rootPath,
        SimpleLibraryInfo library,
        IDictionary<string, ClassPathCandidate> selectedCandidates,
        IList<string> candidateOrder)
    {
        if (IsNativeLibrary(library))
        {
            return;
        }

        var resolvedPath = ForgeLibraryPathHelper.GetAbsoluteLibraryPath(rootPath, library.Name);
        AddClassPath(resolvedPath, library.Name, selectedCandidates, candidateOrder);
    }

    private static void AddDetailedLibraryClassPath(
        string rootPath,
        LibraryInfo library,
        IDictionary<string, ClassPathCandidate> selectedCandidates,
        IList<string> candidateOrder)
    {
        if (!CompatibilityRuleEvaluator.CheckRulesApply(library.Rules) || IsNativeLibrary(library))
        {
            return;
        }

        var resolvedPath = library.Downloads?.Artifact?.Path ?? library.Path;
        if (!string.IsNullOrWhiteSpace(resolvedPath))
        {
            AddClassPath(
                NormalizeLocalPath(VanillaGameDownloader.GetLibrarySavePath(rootPath, resolvedPath)),
                library.Name,
                selectedCandidates,
                candidateOrder);
            return;
        }

        if (!VanillaGameDownloader.TryBuildMavenRelativePath(library.Name, out var relativePath))
        {
            return;
        }

        AddClassPath(
            NormalizeLocalPath(VanillaGameDownloader.GetLibrarySavePath(rootPath, relativePath)),
            library.Name,
            selectedCandidates,
            candidateOrder);
    }

    private static void AddClassPath(
        string path,
        string libraryName,
        IDictionary<string, ClassPathCandidate> selectedCandidates,
        IList<string> candidateOrder)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }
        
        var (dependencyKey, version) = ParseDependencyIdentity(libraryName, path);
        var candidate = new ClassPathCandidate(dependencyKey, version, path);

        if (!selectedCandidates.TryGetValue(dependencyKey, out var existingCandidate))
        {
            selectedCandidates[dependencyKey] = candidate;
            candidateOrder.Add(dependencyKey);
            return;
        }

        if (CompareDependencyVersions(candidate.Version, existingCandidate.Version) > 0)
        {
            selectedCandidates[dependencyKey] = candidate;
        }
    }

    private static string NormalizeLocalPath(string path)
    {
        return path.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
    }

    private static (string Key, string? Version) ParseDependencyIdentity(string libraryName, string fallbackPath)
    {
        if (TryParseMavenCoordinate(libraryName, out var key, out var version))
        {
            return (key, version);
        }

        return (fallbackPath, null);
    }

    private static bool TryParseMavenCoordinate(string libraryName, out string key, out string? version)
    {
        key = string.Empty;
        version = null;

        if (string.IsNullOrWhiteSpace(libraryName))
        {
            return false;
        }

        var parts = libraryName.Split(':', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 3)
        {
            return false;
        }

        version = parts[2];
        key = parts.Length == 3
            ? $"{parts[0]}:{parts[1]}"
            : $"{parts[0]}:{parts[1]}:{string.Join(':', parts.Skip(3))}";
        return true;
    }

    private static int CompareDependencyVersions(string? left, string? right)
    {
        if (string.Equals(left, right, StringComparison.OrdinalIgnoreCase))
        {
            return 0;
        }

        if (string.IsNullOrWhiteSpace(left))
        {
            return -1;
        }

        if (string.IsNullOrWhiteSpace(right))
        {
            return 1;
        }

        var leftSegments = SplitVersionSegments(left);
        var rightSegments = SplitVersionSegments(right);
        var segmentCount = Math.Max(leftSegments.Count, rightSegments.Count);
        for (var i = 0; i < segmentCount; i++)
        {
            var leftSegment = i < leftSegments.Count ? leftSegments[i] : "0";
            var rightSegment = i < rightSegments.Count ? rightSegments[i] : "0";

            var leftIsNumber = int.TryParse(leftSegment, out var leftNumber);
            var rightIsNumber = int.TryParse(rightSegment, out var rightNumber);
            int comparison;

            if (leftIsNumber && rightIsNumber)
            {
                comparison = leftNumber.CompareTo(rightNumber);
            }
            else if (leftIsNumber != rightIsNumber)
            {
                comparison = leftIsNumber ? 1 : -1;
            }
            else
            {
                comparison = string.Compare(leftSegment, rightSegment, StringComparison.OrdinalIgnoreCase);
            }

            if (comparison != 0)
            {
                return comparison;
            }
        }

        return string.Compare(left, right, StringComparison.OrdinalIgnoreCase);
    }

    private static IReadOnlyList<string> SplitVersionSegments(string version)
    {
        return version
            .Split(['.', '-', '+', '_'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();
    }

    private static bool IsNativeLibrary(ILibraryInfo libraryInfo)
    {
        return libraryInfo switch
        {
            SimpleLibraryInfo => false,
            LibraryInfo libInfo => libInfo.Natives != null && libInfo.Natives.ContainsKey(PlatformDetector.GetCurrentOs()),
            _ => false
        };
    }

    private sealed record ClassPathCandidate(string DependencyKey, string? Version, string Path);
}
