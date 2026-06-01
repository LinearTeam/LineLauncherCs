using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using LMCCore.Game.Model;
using LMCCore.Game.Versioning;

namespace LMCUI.Pages.VersionManagePage;

internal enum VersionDisplayType
{
    Release,
    Snapshot,
    AprilFools,
    Old,
    Error
}

internal enum VersionIconKind
{
    Asset,
    File,
    Symbol
}

internal sealed class VersionRenderData
{
    public required LocalGameVersionEntry Version { get; init; }
    public required VersionDisplayType DisplayType { get; init; }
    public required string Description { get; init; }
    public required VersionIconKind IconKind { get; init; }
    public string? IconPath { get; init; }
}

internal sealed record RootListDisplayItem(ManagedGameRoot Root, string Header, string Description);
internal sealed record InvalidVersionNotification(string Signature, IReadOnlyList<string> Items);

internal sealed class VersionManagePageRefreshState
{
    private int _debounceToken;

    public bool PendingExternalRefresh { get; private set; }
    public DateTime LastRefreshUtc { get; private set; } = DateTime.MinValue;

    public int QueueExternalRefresh()
    {
        PendingExternalRefresh = true;
        return ++_debounceToken;
    }

    public bool TryConsumeDebouncedRefresh(int token, bool isPageLoaded)
    {
        return isPageLoaded && token == _debounceToken;
    }

    public void MarkRefreshed(DateTime utcNow)
    {
        PendingExternalRefresh = false;
        LastRefreshUtc = utcNow;
    }

    public void ClearPendingExternalRefresh()
    {
        PendingExternalRefresh = false;
    }

    public bool ShouldRefreshOnActivation(bool isPageLoaded, DateTime currentUtc)
    {
        return VersionManagePagePresentation.ShouldRefreshOnActivation(
            isPageLoaded,
            PendingExternalRefresh,
            LastRefreshUtc,
            currentUtc);
    }
}

internal static class VersionManagePagePresentation
{
    public static VersionDisplayType GetDisplayType(LocalGameVersionEntry version)
    {
        if (version.Status != VersionStatus.Valid)
        {
            return VersionDisplayType.Error;
        }

        var displayType = version.VersionInfo == null
            ? GameVersionDisplayType.Release
            : GameVersionTypeClassifier.ClassifyManifestVersion(
                version.VersionInfo.Id,
                version.VersionInfo.Type,
                version.VersionInfo.ReleaseTime,
                version.VersionName,
                version.ClientVersionId);

        return displayType switch
        {
            GameVersionDisplayType.Snapshot => VersionDisplayType.Snapshot,
            GameVersionDisplayType.AprilFools => VersionDisplayType.AprilFools,
            GameVersionDisplayType.Old => VersionDisplayType.Old,
            _ => VersionDisplayType.Release
        };
    }

    public static VersionRenderData BuildVersionRenderData(
        LocalGameVersionEntry version,
        Func<VersionDisplayType, string> getDisplayTypeText,
        string unknownClientVersionText,
        Func<VersionDisplayType, LocalGameVersionEntry, (VersionIconKind IconKind, string? IconPath)> resolveVersionIcon)
    {
        var displayType = GetDisplayType(version);
        var (iconKind, iconPath) = resolveVersionIcon(displayType, version);

        return new VersionRenderData
        {
            Version = version,
            DisplayType = displayType,
            Description = $"{getDisplayTypeText(displayType)} - {GetClientVersionIdText(version.ClientVersionId, unknownClientVersionText)}",
            IconKind = iconKind,
            IconPath = iconPath
        };
    }

    public static string BuildCurrentRootDescription(ManagedGameRoot selectedRoot, string versionCountText)
    {
        return $"{selectedRoot.RootPath} | {versionCountText}";
    }

    public static IReadOnlyList<RootListDisplayItem> BuildRootListItems(IEnumerable<ManagedGameRoot> roots)
    {
        return roots.Select(root => new RootListDisplayItem(
                root,
                Path.GetFileName(root.RootPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)),
                root.RootPath))
            .ToList()
            .AsReadOnly();
    }

    public static int GetSelectedRootIndex(IReadOnlyList<RootListDisplayItem> items, ManagedGameRoot? selectedRoot)
    {
        if (selectedRoot == null)
        {
            return items.Count > 0 ? 0 : -1;
        }

        for (var index = 0; index < items.Count; index++)
        {
            if (string.Equals(items[index].Root.RootPath, selectedRoot.RootPath, StringComparison.OrdinalIgnoreCase))
            {
                return index;
            }
        }

        return items.Count > 0 ? 0 : -1;
    }

    public static string BuildInvalidVersionSignature(IEnumerable<string> invalidVersions)
    {
        return string.Join("|", invalidVersions);
    }

    public static InvalidVersionNotification? BuildInvalidVersionNotification(
        IEnumerable<LocalGameVersionEntry> versions,
        Func<VersionStatus, string> getStatusText)
    {
        var invalidVersions = versions
            .Where(version => version.Status != VersionStatus.Valid)
            .Select(version => $"{version.VersionName} ({getStatusText(version.Status)})")
            .ToList();

        if (invalidVersions.Count == 0)
        {
            return null;
        }

        return new InvalidVersionNotification(
            BuildInvalidVersionSignature(invalidVersions),
            invalidVersions);
    }

    public static (VersionIconKind IconKind, string? IconPath) ResolveVersionIcon(
        VersionDisplayType displayType,
        string? customIconPath)
    {
        if (displayType != VersionDisplayType.Error && IsValidIconFile(customIconPath))
        {
            return (VersionIconKind.File, customIconPath);
        }

        var assetPath = GetBuiltInIconResourcePath(displayType);
        return !string.IsNullOrWhiteSpace(assetPath)
            ? (VersionIconKind.Asset, assetPath)
            : (VersionIconKind.Symbol, null);
    }

    public static string GetBuiltInIconResourcePath(VersionDisplayType displayType)
    {
        return displayType switch
        {
            VersionDisplayType.Snapshot => "/Assets/VersionIcons/snapshot.png",
            VersionDisplayType.AprilFools => "/Assets/VersionIcons/aprilfools.png",
            VersionDisplayType.Old => "/Assets/VersionIcons/old.png",
            VersionDisplayType.Error => string.Empty,
            _ => "/Assets/VersionIcons/release.png"
        };
    }

    public static string NormalizeRootPath(string rootPath)
    {
        return Path.GetFullPath(rootPath)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
    }

    public static bool ShouldRefreshOnActivation(bool isPageLoaded, bool pendingExternalRefresh, DateTime lastRefreshUtc, DateTime currentUtc)
    {
        return isPageLoaded && (pendingExternalRefresh || currentUtc - lastRefreshUtc > TimeSpan.FromSeconds(2));
    }

    public static bool IsValidIconFile(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        try
        {
            return File.Exists(path) && Uri.TryCreate(path, UriKind.Absolute, out _);
        }
        catch
        {
            return false;
        }
    }

    private static string GetClientVersionIdText(string clientVersionId, string unknownClientVersionText)
    {
        return string.Equals(clientVersionId, LocalGameVersionEntry.UnknownClientVersionId, StringComparison.Ordinal)
            ? unknownClientVersionText
            : clientVersionId;
    }
}
