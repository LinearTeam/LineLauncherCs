using LMCCore.Game.Model;
using LMCCore.Game.Versioning.Configuration.Support;
using LMCCore.Utils;

namespace LMCCore.Game.Versioning.Configuration;

public interface IVersionConfigSource
{
    VersionConfigSourceType SourceType { get; }

    JsonUtils? TryLoad(LocalGameVersionEntry version, VersionConfigFileCache cache);
}
