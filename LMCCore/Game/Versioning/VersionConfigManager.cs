using LMC;
using LMCCore.Game.Model;
using LMCCore.Utils;

namespace LMCCore.Game.Versioning;

public class VersionConfigManager
{
    private readonly IReadOnlyList<IVersionConfigSource> _configSources;
    private readonly string _globalConfigPath;

    public VersionConfigManager(IEnumerable<IVersionConfigSource>? configSources = null, string? globalConfigPath = null)
    {
        _configSources = (configSources ?? CreateDefaultSources()).ToList().AsReadOnly();
        _globalConfigPath = globalConfigPath ?? Path.Combine(Current.LMCPath, "global_version_config.json");
    }

    public VersionConfigSourceType GetConfigSource(LocalGameVersionEntry version)
    {
        return GetIndependentConfigSourceResult(version).SourceType;
    }

    public JsonUtils? GetIndependentConfig(LocalGameVersionEntry version)
    {
        return GetIndependentConfigSourceResult(version).Config;
    }

    public JsonUtils? GetEffectiveConfig(LocalGameVersionEntry version)
    {
        var independentConfig = GetIndependentConfig(version);
        var globalConfig = LoadGlobalConfig();

        if (independentConfig is { IsValid: true, Node: not null } && globalConfig is { IsValid: true, Node: not null })
        {
            return independentConfig.Merge(globalConfig);
        }

        if (independentConfig is { IsValid: true, Node: not null })
        {
            return independentConfig.Clone();
        }

        if (globalConfig is { IsValid: true, Node: not null })
        {
            return globalConfig.Clone();
        }

        return null;
    }

    public T? GetValue<T>(LocalGameVersionEntry version, string path, T? defaultValue = default)
    {
        var independentConfig = GetIndependentConfig(version);
        if (independentConfig is { IsValid: true } && independentConfig.HasValue(path))
        {
            return independentConfig.GetOrDefault(path, defaultValue);
        }

        var globalConfig = LoadGlobalConfig();
        if (globalConfig is { IsValid: true } && globalConfig.HasValue(path))
        {
            return globalConfig.GetOrDefault(path, defaultValue);
        }

        return defaultValue;
    }

    private VersionConfigSourceResult GetIndependentConfigSourceResult(LocalGameVersionEntry version)
    {
        foreach (var source in _configSources)
        {
            var config = source.TryLoad(version);
            if (config is not { IsValid: true, Node: not null })
            {
                continue;
            }

            return new VersionConfigSourceResult
            {
                SourceType = source.SourceType,
                Config = config
            };
        }

        return new VersionConfigSourceResult
        {
            SourceType = VersionConfigSourceType.None,
            Config = null
        };
    }

    private JsonUtils? LoadGlobalConfig()
    {
        if (!File.Exists(_globalConfigPath))
        {
            return null;
        }

        var json = JsonUtils.Parse(File.ReadAllText(_globalConfigPath));
        return json.IsValid ? json : null;
    }

    private static IEnumerable<IVersionConfigSource> CreateDefaultSources()
    {
        yield return new VersionJsonConfigSource();
        yield return new VersionFolderConfigSource();
        yield return new LMCDataDirectoryConfigSource();
    }
}
