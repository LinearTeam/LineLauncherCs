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
using System.Text.Json;
using System.Text.Json.Nodes;
using LMC.Basic.Configs;
using LMC;
using LMCCore.Game.Model;
using LMCCore.Game.Model.Configuration;
using LMCCore.Game.Versioning.Configuration;
using LMCCore.Game.Versioning.Configuration.Support;
using LMCCore.Utils;

namespace LMCCore.Game.Versioning;

public class VersionConfigManager(
    IEnumerable<IVersionConfigSource>? configSources = null,
    string? globalConfigPath = null,
    string? versionConfigsPath = null)
{
    private readonly IReadOnlyList<IVersionConfigSource> _configSources = (configSources ?? CreateDefaultSources()).ToList().AsReadOnly();
    private readonly string _globalConfigPath = globalConfigPath ?? Path.Combine(Current.LMCPath, "global_version_config.json");
    private readonly string _versionConfigsPath = versionConfigsPath ?? Path.Combine(Current.LMCPath, "version_configs.json");
    private readonly VersionConfigFileCache _fileCache = new();
    private const string VersionJsonConfigPropertyName = "LMCConfig";
    private const string VersionFolderConfigDirectoryName = "LMC";
    private const string VersionFolderConfigFileName = "version_config.json";

    public VersionConfigSourceType GetConfigSource(LocalGameVersionEntry version)
    {
        return GetIndependentConfigSourceResult(version).SourceType;
    }

    public JsonUtils? GetIndependentConfig(LocalGameVersionEntry version)
    {
        return GetIndependentConfigSourceResult(version).Config;
    }

    public GameVersionConfig? GetIndependentConfigModel(LocalGameVersionEntry version)
    {
        return GetIndependentConfig(version)?.Get<GameVersionConfig>();
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

    public GameVersionConfig? GetEffectiveConfigModel(LocalGameVersionEntry version)
    {
        return GetEffectiveConfig(version)?.Get<GameVersionConfig>();
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

    public void WriteIndependentConfig(
        LocalGameVersionEntry version,
        GameVersionConfig config,
        NewVersionConfigSource source)
    {
        ArgumentNullException.ThrowIfNull(config);
        WriteIndependentConfig(version, SerializeConfigModel(config), source);
    }

    public void WriteIndependentConfig(
        LocalGameVersionEntry version,
        GameVersionConfig config,
        VersionConfigSourceType source)
    {
        ArgumentNullException.ThrowIfNull(config);
        WriteIndependentConfig(version, SerializeConfigModel(config), source);
    }

    public void WriteIndependentConfig(
        LocalGameVersionEntry version,
        JsonNode config,
        NewVersionConfigSource source)
    {
        WriteIndependentConfig(version, config, MapToVersionConfigSourceType(source));
    }

    public void WriteIndependentConfig(
        LocalGameVersionEntry version,
        JsonNode config,
        VersionConfigSourceType source)
    {
        ArgumentNullException.ThrowIfNull(version);
        ArgumentNullException.ThrowIfNull(config);

        var configObject = CloneConfigObject(config);
        switch (source)
        {
            case VersionConfigSourceType.VersionJson:
                WriteVersionJsonConfig(version, configObject);
                break;
            case VersionConfigSourceType.VersionFolder:
                WriteVersionFolderConfig(version, configObject);
                break;
            case VersionConfigSourceType.LMCDataDirectory:
                WriteLmcDataDirectoryConfig(version, configObject);
                break;
            case VersionConfigSourceType.None:
            case VersionConfigSourceType.Global:
            default:
                throw new NotSupportedException($"Writing version config to '{source}' is not supported.");
        }
    }

    private VersionConfigSourceResult GetIndependentConfigSourceResult(LocalGameVersionEntry version)
    {
        foreach (var source in _configSources)
        {
            var config = source.TryLoad(version, _fileCache);
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
        var json = _fileCache.GetOrAdd(_globalConfigPath);
        return json is { IsValid: true } ? json : null;
    }

    private void WriteVersionJsonConfig(LocalGameVersionEntry version, JsonObject configObject)
    {
        if (string.IsNullOrWhiteSpace(version.JsonPath))
        {
            throw new InvalidOperationException("The target version does not have a version json path.");
        }

        var normalizedJsonPath = VersionPathUtils.NormalizePath(version.JsonPath);
        if (!File.Exists(normalizedJsonPath))
        {
            throw new FileNotFoundException("The target version json file was not found.", normalizedJsonPath);
        }

        if (JsonNode.Parse(File.ReadAllText(normalizedJsonPath)) is not JsonObject versionJsonObject)
        {
            throw new InvalidOperationException($"The version json '{normalizedJsonPath}' is not a valid json object.");
        }

        versionJsonObject[VersionJsonConfigPropertyName] = configObject;
        File.WriteAllText(normalizedJsonPath, versionJsonObject.ToJsonString(JsonUtils.DefaultSerializerOptions));
    }

    private static void WriteVersionFolderConfig(LocalGameVersionEntry version, JsonObject configObject)
    {
        var configDirectory = Path.Combine(version.VersionDirectory, VersionFolderConfigDirectoryName);
        Directory.CreateDirectory(configDirectory);

        var configPath = Path.Combine(configDirectory, VersionFolderConfigFileName);
        File.WriteAllText(configPath, configObject.ToJsonString(JsonUtils.DefaultSerializerOptions));
    }

    private void WriteLmcDataDirectoryConfig(LocalGameVersionEntry version, JsonObject configObject)
    {
        var normalizedConfigPath = VersionPathUtils.NormalizePath(_versionConfigsPath);
        JsonObject rootObject;

        if (File.Exists(normalizedConfigPath))
        {
            if (JsonNode.Parse(File.ReadAllText(normalizedConfigPath)) is not JsonObject parsedRootObject)
            {
                throw new InvalidOperationException($"The version config file '{normalizedConfigPath}' is not a valid json object.");
            }

            rootObject = parsedRootObject;
        }
        else
        {
            rootObject = [];
        }

        var normalizedVersionDirectory = VersionPathUtils.NormalizePath(version.VersionDirectory);
        rootObject[normalizedVersionDirectory] = configObject;

        Directory.CreateDirectory(Path.GetDirectoryName(normalizedConfigPath)!);
        File.WriteAllText(normalizedConfigPath, rootObject.ToJsonString(JsonUtils.DefaultSerializerOptions));
    }

    private static JsonObject CloneConfigObject(JsonNode config)
    {
        return config.DeepClone() as JsonObject
               ?? throw new ArgumentException("Version config must be a json object.", nameof(config));
    }

    private static JsonNode SerializeConfigModel(GameVersionConfig config)
    {
        return JsonSerializer.SerializeToNode(config, JsonUtils.DefaultSerializerOptions)
               ?? throw new InvalidOperationException("Failed to serialize the version config model.");
    }

    private static VersionConfigSourceType MapToVersionConfigSourceType(NewVersionConfigSource source)
    {
        return source switch
        {
            NewVersionConfigSource.VersionJson => VersionConfigSourceType.VersionJson,
            NewVersionConfigSource.VersionFolder => VersionConfigSourceType.VersionFolder,
            NewVersionConfigSource.LMCDataDirectory => VersionConfigSourceType.LMCDataDirectory,
            _ => throw new NotSupportedException($"Unsupported new version config source '{source}'.")
        };
    }

    private static IEnumerable<IVersionConfigSource> CreateDefaultSources()
    {
        yield return new VersionJsonConfigSource();
        yield return new VersionFolderConfigSource();
        yield return new LMCDataDirectoryConfigSource();
    }
}
