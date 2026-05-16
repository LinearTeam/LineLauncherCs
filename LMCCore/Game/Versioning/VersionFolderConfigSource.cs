using LMCCore.Game.Model;
using LMCCore.Utils;

namespace LMCCore.Game.Versioning;

public class VersionFolderConfigSource : IVersionConfigSource
{
    private const string ConfigDirectoryName = "LMC";
    private const string ConfigFileName = "version_config.json";

    public VersionConfigSourceType SourceType => VersionConfigSourceType.VersionFolder;

    public JsonUtils? TryLoad(LocalGameVersionEntry version, VersionConfigFileCache cache)
    {
        var configPath = Path.Combine(version.VersionDirectory, ConfigDirectoryName, ConfigFileName);
        return cache.GetOrAdd(configPath);
    }
}
