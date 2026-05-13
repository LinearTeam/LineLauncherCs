using LMCCore.Game.Model.LocalVersion;

namespace LMCCore.Game.Model;

public class LocalGameVersionEntry
{
    public const string UnknownClientVersionId = "未知版本";

    public required string RootPath { get; init; }

    public required string VersionName { get; init; }

    public required string VersionDirectory { get; init; }

    public string? JarPath { get; init; }

    public string? JsonPath { get; init; }

    public string ClientVersionId { get; init; } = UnknownClientVersionId;

    public LocalVersionInfo? VersionInfo { get; init; }

    public required VersionStatus Status { get; init; }
    
}
