using LMCCore.Game.Model;
using LMCCore.Utils;

namespace LMCCore.Game.Versioning.Configuration;

public class VersionConfigSourceResult
{
    public required VersionConfigSourceType SourceType { get; init; }

    public JsonUtils? Config { get; init; }
}
