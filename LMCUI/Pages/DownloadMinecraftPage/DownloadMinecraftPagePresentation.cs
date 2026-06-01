using System;
using System.Collections.Generic;
using System.Linq;
using System.Globalization;
using LMCCore.Game.Download.Model.Vanilla;
using LMCCore.Game.Versioning;
using LMCUI.Pages.DownloadMinecraftPage.DownloadMinecraft;

namespace LMCUI.Pages.DownloadMinecraftPage;

internal sealed class ManifestVersionListItem
{
    public required string Id { get; init; }
    public required GameVersionDisplayType DisplayType { get; init; }
    public required string DisplayTypeText { get; init; }
    public required string LocalReleaseTimeText { get; init; }
    public required string Description { get; init; }
    public required VersionEntry Source { get; init; }
}

internal readonly record struct DownloadMinecraftFilterOptions(
    bool ShowRelease,
    bool ShowSnapshot,
    bool ShowAprilFools,
    bool ShowOld);

internal sealed record DownloadMinecraftManifestState(
    IReadOnlyList<ManifestVersionListItem> Versions,
    ManifestVersionListItem? LatestRelease,
    ManifestVersionListItem? LatestSnapshot);

internal static class DownloadMinecraftPagePresentation
{
    public static ManifestVersionListItem CreateVersionItem(
        VersionEntry version,
        Func<GameVersionDisplayType, string> displayTypeTextResolver)
    {
        var displayType = GameVersionTypeClassifier.ClassifyManifestVersion(version);
        var displayTypeText = displayTypeTextResolver(displayType);
        var localReleaseTimeText = version.ReleaseTime.ToLocalTime()
            .ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.CurrentCulture);

        return new ManifestVersionListItem
        {
            Id = version.Id,
            DisplayType = displayType,
            DisplayTypeText = displayTypeText,
            LocalReleaseTimeText = localReleaseTimeText,
            Description = $"{displayTypeText} | {localReleaseTimeText}",
            Source = version
        };
    }

    public static IReadOnlyList<ManifestVersionListItem> FilterVersions(
        IReadOnlyList<ManifestVersionListItem> versions,
        DownloadMinecraftFilterOptions options,
        string? searchText)
    {
        var normalizedSearch = (searchText ?? string.Empty).Trim();
        return versions
            .Where(item => IsVisibleByFilter(item.DisplayType, options))
            .Where(item => string.IsNullOrWhiteSpace(normalizedSearch) ||
                           item.Id.Contains(normalizedSearch, StringComparison.OrdinalIgnoreCase))
            .ToList();
    }

    public static IReadOnlyList<string> BuildSearchCandidates(IReadOnlyList<ManifestVersionListItem> versions)
    {
        return versions
            .Select(item => item.Id)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public static DownloadMinecraftManifestState BuildManifestState(
        VersionManifestInfo manifest,
        Func<GameVersionDisplayType, string> displayTypeTextResolver)
    {
        var viewModels = manifest.Versions
            .Select(version => CreateVersionItem(version, displayTypeTextResolver))
            .OrderByDescending(item => item.Source.ReleaseTime)
            .ToList();

        return new DownloadMinecraftManifestState(
            viewModels,
            viewModels.FirstOrDefault(item => string.Equals(item.Id, manifest.Latest.Release, StringComparison.OrdinalIgnoreCase)),
            viewModels.FirstOrDefault(item => string.Equals(item.Id, manifest.Latest.Snapshot, StringComparison.OrdinalIgnoreCase)));
    }

    public static bool CanOpenWizard(string? selectedRootPath)
    {
        return !string.IsNullOrWhiteSpace(selectedRootPath);
    }

    public static DownloadMinecraftWizardContext? TryCreateWizardContext(string? selectedRootPath, string manifestVersionId)
    {
        return CanOpenWizard(selectedRootPath)
            ? new DownloadMinecraftWizardContext(selectedRootPath!, manifestVersionId)
            : null;
    }

    public static string GetBuiltInIconResourcePath(GameVersionDisplayType displayType)
    {
        return displayType switch
        {
            GameVersionDisplayType.Snapshot => "/Assets/VersionIcons/snapshot.png",
            GameVersionDisplayType.AprilFools => "/Assets/VersionIcons/aprilfools.png",
            GameVersionDisplayType.Old => "/Assets/VersionIcons/old.png",
            _ => "/Assets/VersionIcons/release.png"
        };
    }

    private static bool IsVisibleByFilter(GameVersionDisplayType displayType, DownloadMinecraftFilterOptions options)
    {
        return displayType switch
        {
            GameVersionDisplayType.Release => options.ShowRelease,
            GameVersionDisplayType.Snapshot => options.ShowSnapshot,
            GameVersionDisplayType.AprilFools => options.ShowAprilFools,
            GameVersionDisplayType.Old => options.ShowOld,
            _ => true
        };
    }
}
