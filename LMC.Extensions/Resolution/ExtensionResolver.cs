using LMC.Extensions.Packaging;
using LMC.Extensions.Runtime;
using LMC.Extensions.Abstractions;
using NuGet.Versioning;

namespace LMC.Extensions.Resolution;

public sealed class ExtensionResolver : IExtensionResolver
{
    private readonly ILMCExtensionLogger _logger;

    public ExtensionResolver(ILMCExtensionLogger? logger = null)
    {
        _logger = logger ?? NullLMCExtensionLoggerFactory.Instance.CreateLogger(nameof(ExtensionResolver));
    }

    public ExtensionResolutionResult Resolve(IReadOnlyList<ExtensionPackageInfo> packages, string hostVersion)
    {
        _logger.Info($"开始解析扩展依赖关系，宿主版本：{hostVersion}，候选扩展包：{packages.Count} 个。");
        var unresolved = new List<UnresolvedExtension>();
        if (!NuGetVersion.TryParse(hostVersion, out var currentHostVersion))
        {
            _logger.Warn($"宿主版本号无法解析：{hostVersion}，将按 0.0.0 处理兼容性。");
            currentHostVersion = new NuGetVersion(0, 0, 0);
        }

        var selectedPackages = new Dictionary<string, ExtensionPackageInfo>(StringComparer.OrdinalIgnoreCase);
        foreach (var groupedPackages in packages.GroupBy(package => package.Manifest.Id, StringComparer.OrdinalIgnoreCase))
        {
            var compatiblePackages = groupedPackages
                .Where(package => IsLauncherCompatible(package.Manifest, currentHostVersion))
                .OrderByDescending(package => NuGetVersion.Parse(package.Manifest.Version), VersionComparer.VersionRelease)
                .ToList();

            if (compatiblePackages.Count == 0)
            {
                foreach (var package in groupedPackages)
                {
                    _logger.Warn($"扩展与当前启动器版本不兼容，已跳过：{package.Manifest.Id} {package.Manifest.Version}，要求：{package.Manifest.LauncherVersionRange ?? "未声明"}，当前：{hostVersion}");
                    unresolved.Add(new UnresolvedExtension
                    {
                        ExtensionId = package.Manifest.Id,
                        Version = package.Manifest.Version,
                        PackagePath = package.PackagePath,
                        Status = ExtensionLoadStatus.SkippedIncompatibleLauncher,
                        Message = $"Extension {package.Manifest.Id} is not compatible with host version {hostVersion}."
                    });
                }

                continue;
            }

            _logger.Debug($"扩展已选定兼容版本：{groupedPackages.Key} -> {compatiblePackages[0].Manifest.Version}");
            selectedPackages[groupedPackages.Key] = compatiblePackages[0];
        }

        var validPackages = new Dictionary<string, ExtensionPackageInfo>(selectedPackages, StringComparer.OrdinalIgnoreCase);
        var softDependencyLookup = BuildSoftDependencyLookup(validPackages);

        var changed = true;
        while (changed)
        {
            changed = false;
            foreach (var package in validPackages.Values.ToList())
            {
                if (TryGetInvalidHardDependency(package, validPackages, out var error))
                {
                    _logger.Warn($"扩展硬依赖不满足，已跳过：{package.Manifest.Id} {package.Manifest.Version}，原因：{error}");
                    validPackages.Remove(package.Manifest.Id);
                    unresolved.Add(new UnresolvedExtension
                    {
                        ExtensionId = package.Manifest.Id,
                        Version = package.Manifest.Version,
                        PackagePath = package.PackagePath,
                        Status = ExtensionLoadStatus.SkippedMissingHardDependency,
                        Message = error
                    });
                    changed = true;
                }
            }
        }

        var hardDependencies = BuildHardDependencyLookup(validPackages);
        var remaining = new HashSet<string>(validPackages.Keys, StringComparer.OrdinalIgnoreCase);
        var indegree = BuildIndegreeLookup(hardDependencies);
        var resolvedOrder = new List<string>();

        while (remaining.Count > 0)
        {
            var available = remaining
                .Where(id => indegree.GetValueOrDefault(id) == 0)
                .OrderByDescending(id => CountSoftDependents(id, remaining, softDependencyLookup))
                .ThenBy(id => id, StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (available.Count == 0)
            {
                foreach (var extensionId in remaining.OrderBy(id => id, StringComparer.OrdinalIgnoreCase))
                {
                    var package = validPackages[extensionId];
                    _logger.Warn($"检测到扩展硬依赖环，已跳过：{package.Manifest.Id} {package.Manifest.Version}");
                    unresolved.Add(new UnresolvedExtension
                    {
                        ExtensionId = package.Manifest.Id,
                        Version = package.Manifest.Version,
                        PackagePath = package.PackagePath,
                        Status = ExtensionLoadStatus.SkippedMissingHardDependency,
                        Message = $"Hard dependency cycle detected for extension {package.Manifest.Id}."
                    });
                }

                break;
            }

            foreach (var extensionId in available)
            {
                remaining.Remove(extensionId);
                resolvedOrder.Add(extensionId);

                foreach (var dependentId in hardDependencies
                             .Where(pair => pair.Value.Contains(extensionId, StringComparer.OrdinalIgnoreCase))
                             .Select(pair => pair.Key))
                {
                    indegree[dependentId]--;
                }
            }
        }

        var resolvedById = new Dictionary<string, ResolvedExtension>(StringComparer.OrdinalIgnoreCase);
        foreach (var extensionId in resolvedOrder)
        {
            var package = validPackages[extensionId];
            LogSoftDependencyIssues(package, resolvedById);
            resolvedById[extensionId] = new ResolvedExtension
            {
                Package = package,
                HardDependencies = package.Manifest.Dependencies
                    .Where(dependency => dependency.IsHardDependency)
                    .Select(dependency => resolvedById[dependency.Id])
                    .ToList()
                    .AsReadOnly(),
                SoftDependencies = package.Manifest.Dependencies
                    .Where(dependency => !dependency.IsHardDependency)
                    .Where(dependency => resolvedById.ContainsKey(dependency.Id) &&
                                         IsDependencyVersionSatisfied(resolvedById[dependency.Id].Package.Manifest.Version, dependency.VersionRange))
                    .Select(dependency => resolvedById[dependency.Id])
                    .ToList()
                    .AsReadOnly()
            };
        }

        _logger.Info($"扩展依赖解析完成：可加载 {resolvedOrder.Count} 个，已跳过 {unresolved.Count} 个。");
        return new ExtensionResolutionResult
        {
            ResolvedExtensions = resolvedOrder
                .Where(resolvedById.ContainsKey)
                .Select(id => resolvedById[id])
                .ToList()
                .AsReadOnly(),
            UnresolvedExtensions = unresolved.AsReadOnly()
        };
    }

    private static bool IsLauncherCompatible(ExtensionManifest manifest, NuGetVersion currentHostVersion)
    {
        if (string.IsNullOrWhiteSpace(manifest.LauncherVersionRange))
        {
            return true;
        }

        if (!VersionRange.TryParse(manifest.LauncherVersionRange, out var versionRange))
        {
            return false;
        }

        return versionRange.Satisfies(currentHostVersion);
    }

    private static bool TryGetInvalidHardDependency(
        ExtensionPackageInfo package,
        IReadOnlyDictionary<string, ExtensionPackageInfo> selectedPackages,
        out string error)
    {
        foreach (var dependency in package.Manifest.Dependencies.Where(item => item.IsHardDependency))
        {
            if (!selectedPackages.TryGetValue(dependency.Id, out var candidate))
            {
                error = $"Hard dependency {dependency.Id} is missing for extension {package.Manifest.Id}.";
                return true;
            }

            if (!IsDependencyVersionSatisfied(candidate.Manifest.Version, dependency.VersionRange))
            {
                error = $"Hard dependency {dependency.Id} does not satisfy {dependency.VersionRange} for extension {package.Manifest.Id}.";
                return true;
            }
        }

        error = string.Empty;
        return false;
    }

    private static bool IsDependencyVersionSatisfied(string version, string versionRange)
    {
        return NuGetVersion.TryParse(version, out var parsedVersion) &&
               VersionRange.TryParse(versionRange, out var parsedRange) &&
               parsedRange.Satisfies(parsedVersion);
    }

    private static Dictionary<string, HashSet<string>> BuildHardDependencyLookup(IReadOnlyDictionary<string, ExtensionPackageInfo> packages)
    {
        var result = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var pair in packages)
        {
            result[pair.Key] = pair.Value.Manifest.Dependencies
                .Where(dependency => dependency.IsHardDependency && packages.ContainsKey(dependency.Id))
                .Select(dependency => dependency.Id)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
        }

        return result;
    }

    private static Dictionary<string, List<string>> BuildSoftDependencyLookup(IReadOnlyDictionary<string, ExtensionPackageInfo> packages)
    {
        var result = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var pair in packages)
        {
            result[pair.Key] = pair.Value.Manifest.Dependencies
                .Where(dependency => !dependency.IsHardDependency)
                .Select(dependency => dependency.Id)
                .ToList();
        }

        return result;
    }

    private static Dictionary<string, int> BuildIndegreeLookup(IReadOnlyDictionary<string, HashSet<string>> hardDependencies)
    {
        return hardDependencies.ToDictionary(pair => pair.Key, pair => pair.Value.Count, StringComparer.OrdinalIgnoreCase);
    }

    private void LogSoftDependencyIssues(
        ExtensionPackageInfo package,
        IReadOnlyDictionary<string, ResolvedExtension> resolvedById)
    {
        foreach (var dependency in package.Manifest.Dependencies.Where(dependency => !dependency.IsHardDependency))
        {
            if (!resolvedById.TryGetValue(dependency.Id, out var resolvedDependency))
            {
                _logger.Warn($"扩展 {package.Manifest.Id} 的软依赖 {dependency.Id} 未加载，将继续加载该扩展。");
                continue;
            }

            if (!IsDependencyVersionSatisfied(resolvedDependency.Package.Manifest.Version, dependency.VersionRange))
            {
                _logger.Warn($"扩展 {package.Manifest.Id} 的软依赖 {dependency.Id} 版本不满足 {dependency.VersionRange}，当前版本 {resolvedDependency.Package.Manifest.Version}，将继续加载该扩展。");
            }
        }
    }

    private static int CountSoftDependents(
        string extensionId,
        IReadOnlySet<string> remaining,
        IReadOnlyDictionary<string, List<string>> softDependencyLookup)
    {
        return softDependencyLookup.Count(pair =>
            remaining.Contains(pair.Key) &&
            pair.Value.Contains(extensionId, StringComparer.OrdinalIgnoreCase));
    }
}
