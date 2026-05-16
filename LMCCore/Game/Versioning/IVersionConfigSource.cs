using LMCCore.Game.Model;
using LMCCore.Utils;

namespace LMCCore.Game.Versioning;

public interface IVersionConfigSource
{
    VersionConfigSourceType SourceType { get; }

    JsonUtils? TryLoad(LocalGameVersionEntry version, VersionConfigFileCache cache);
}
