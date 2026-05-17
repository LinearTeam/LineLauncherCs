using System.Text.Json.Nodes;
using LMCCore.Game.Model;
using LMCCore.Utils;

namespace LMCCore.Game.Versioning;

public class VersionJsonConfigSource : IVersionConfigSource
{
    private const string ConfigPropertyName = "LMCConfig";

    public VersionConfigSourceType SourceType => VersionConfigSourceType.VersionJson;

    public JsonUtils? TryLoad(LocalGameVersionEntry version, VersionConfigFileCache cache)
    {
        if (string.IsNullOrWhiteSpace(version.JsonPath) || !File.Exists(version.JsonPath))
        {
            return null;
        }

        var json = cache.GetOrAdd(version.JsonPath);
        if (json is not { IsValid: true, Node: JsonObject jsonObject })
        {
            return null;
        }

        if (!jsonObject.TryGetPropertyValue(ConfigPropertyName, out var configNode) || configNode is not JsonObject)
        {
            return null;
        }

        return JsonUtils.Parse(configNode.ToJsonString(JsonUtils.DefaultSerializerOptions));
    }
}
