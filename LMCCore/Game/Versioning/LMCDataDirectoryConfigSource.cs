using System.Text.Json.Nodes;
using LMC;
using LMCCore.Game.Model;
using LMCCore.Utils;

namespace LMCCore.Game.Versioning;

public class LMCDataDirectoryConfigSource : IVersionConfigSource
{
    private readonly string _configFilePath;

    public LMCDataDirectoryConfigSource(string? configFilePath = null)
    {
        _configFilePath = configFilePath ?? Path.Combine(Current.LMCPath, "version_configs.json");
    }

    public VersionConfigSourceType SourceType => VersionConfigSourceType.LMCDataDirectory;

    public JsonUtils? TryLoad(LocalGameVersionEntry version, VersionConfigFileCache cache)
    {
        var json = cache.GetOrAdd(_configFilePath);
        if (json is not { IsValid: true, Node: JsonObject jsonObject })
        {
            return null;
        }

        var normalizedVersionDirectory = VersionPathUtils.NormalizePath(version.VersionDirectory);

        foreach (var property in jsonObject)
        {
            if (!string.Equals(VersionPathUtils.NormalizePath(property.Key), normalizedVersionDirectory, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (property.Value is not JsonObject valueObject)
            {
                return null;
            }

            return JsonUtils.Parse(valueObject.ToJsonString(JsonUtils.DefaultSerializerOptions));
        }

        return null;
    }
}
