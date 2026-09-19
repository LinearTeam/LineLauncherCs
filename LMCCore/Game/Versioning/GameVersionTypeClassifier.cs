using LMCCore.Game.Download.Model.Vanilla;
using LMCCore.Game.Model;
using LMCCore.Game.Model.LocalVersion;

namespace LMCCore.Game.Versioning;

public enum GameVersionDisplayType
{
    Release,
    Snapshot,
    AprilFools,
    Old
}

public static class GameVersionTypeClassifier
{
    private readonly static TimeSpan AprilFoolsOffset = TimeSpan.FromHours(2);
    private readonly static HashSet<string> SnapshotTypeExclusions = new(StringComparer.OrdinalIgnoreCase)
    {
        "26w14a",
        "25w14craftmine",
        "24w14potato",
        "23w13a_or_b",
        "22w13oneblockatetime",
        "20w14∞",
        "20w14infinite",
        "15w14a"
    };

    public static GameVersionDisplayType ClassifyLocalVersion(LocalVersionInfo versionInfo, string versionName, string clientVersionId)
    {
        if (versionInfo.ReleaseTime is { Year: < 2013 and > 2000 })
        {
            return GameVersionDisplayType.Old;
        }

        return ClassifyManifestVersion(versionInfo.Id, versionInfo.Type, versionInfo.ReleaseTime, versionName, clientVersionId);
    }

    public static GameVersionDisplayType ClassifyManifestVersion(VersionEntry versionEntry)
    {
        ArgumentNullException.ThrowIfNull(versionEntry);
        return ClassifyManifestVersion(versionEntry.Id, versionEntry.Type, versionEntry.ReleaseTime);
    }

    public static GameVersionDisplayType ClassifyManifestVersion(
        string? versionId,
        string? manifestType,
        DateTimeOffset? releaseTime,
        string? fallbackVersionName = null,
        string? clientVersionId = null)
    {
        if (string.Equals(manifestType, "old_alpha", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(manifestType, "old_beta", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(manifestType, "old", StringComparison.OrdinalIgnoreCase))
        {
            return GameVersionDisplayType.Old;
        }

        if (string.Equals(manifestType, "release", StringComparison.OrdinalIgnoreCase))
        {
            return GameVersionDisplayType.Release;
        }

        if (string.Equals(manifestType, "april_fools", StringComparison.OrdinalIgnoreCase))
        {
            return GameVersionDisplayType.AprilFools;
        }

        if (string.Equals(manifestType, "snapshot", StringComparison.OrdinalIgnoreCase))
        {
            return ShouldTreatAsAprilFools(versionId, releaseTime, fallbackVersionName, clientVersionId)
                ? GameVersionDisplayType.AprilFools
                : GameVersionDisplayType.Snapshot;
        }

        if (ShouldTreatAsSnapshot(clientVersionId ?? versionId))
        {
            return ShouldTreatAsAprilFools(versionId, releaseTime, fallbackVersionName, clientVersionId)
                ? GameVersionDisplayType.AprilFools
                : GameVersionDisplayType.Snapshot;
        }

        return GameVersionDisplayType.Release;
    }

    public static string NormalizeLocalVersionType(LocalVersionInfo versionInfo, string versionName, string clientVersionId)
    {
        return ClassifyLocalVersion(versionInfo, versionName, clientVersionId) switch
        {
            GameVersionDisplayType.Release => "release",
            GameVersionDisplayType.Snapshot => "snapshot",
            GameVersionDisplayType.AprilFools => "april_fools",
            GameVersionDisplayType.Old => "old",
            _ => versionInfo.Type ?? "release"
        };
    }

    public static bool ShouldTreatAsSnapshot(string? clientVersionId)
    {
        if (string.IsNullOrWhiteSpace(clientVersionId) ||
            string.Equals(clientVersionId, LocalGameVersionEntry.UnknownClientVersionId, StringComparison.Ordinal) ||
            SnapshotTypeExclusions.Contains(clientVersionId))
        {
            return false;
        }

        return clientVersionId.Contains('w', StringComparison.OrdinalIgnoreCase) ||
               clientVersionId.Contains("rc", StringComparison.OrdinalIgnoreCase);
    }

    public static bool ShouldTreatAsAprilFools(
        string? versionId,
        DateTimeOffset? releaseTime,
        string? fallbackVersionName = null,
        string? clientVersionId = null)
    {
        var candidateName = GetAprilFoolsCandidateName(versionId, fallbackVersionName, clientVersionId);
        if (string.IsNullOrWhiteSpace(candidateName))
        {
            return false;
        }

        if (SnapshotTypeExclusions.Contains(candidateName))
        {
            return true;
        }

        if (releaseTime == null || candidateName.All(ch => char.IsDigit(ch) || ch == '.'))
        {
            return false;
        }

        var releaseTimeInUtcPlus2 = releaseTime.Value.ToOffset(AprilFoolsOffset);
        return releaseTimeInUtcPlus2 is { Month: 4, Day: 1 };
    }

    private static string GetAprilFoolsCandidateName(string? versionId, string? fallbackVersionName, string? clientVersionId)
    {
        if (!string.IsNullOrWhiteSpace(clientVersionId) &&
            !string.Equals(clientVersionId, LocalGameVersionEntry.UnknownClientVersionId, StringComparison.Ordinal))
        {
            return clientVersionId;
        }

        if (!string.IsNullOrWhiteSpace(versionId))
        {
            return versionId;
        }

        return fallbackVersionName ?? string.Empty;
    }
}
