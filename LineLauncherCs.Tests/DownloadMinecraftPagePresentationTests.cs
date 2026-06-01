using LMCCore.Game.Download.Model.Vanilla;
using LMCCore.Game.Versioning;
using LMCUI.Pages.DownloadMinecraftPage;
using LMCUI.Pages.DownloadMinecraftPage.DownloadMinecraft;

namespace LineLauncherCs.Tests;

public class DownloadMinecraftPagePresentationTests
{
    [Fact]
    public void FilterVersions_RespectsTypeFlagsAndSearch()
    {
        var items = new[]
        {
            CreateItem("1.20.4", GameVersionDisplayType.Release),
            CreateItem("24w14a", GameVersionDisplayType.Snapshot),
            CreateItem("3D Shareware v1.34", GameVersionDisplayType.AprilFools)
        };

        var filtered = DownloadMinecraftPagePresentation.FilterVersions(
            items,
            new DownloadMinecraftFilterOptions(true, false, true, false),
            "share");

        Assert.Single(filtered);
        Assert.Equal("3D Shareware v1.34", filtered[0].Id);
    }

    [Fact]
    public void BuildSearchCandidates_DeduplicatesIds()
    {
        var candidates = DownloadMinecraftPagePresentation.BuildSearchCandidates(
        [
            CreateItem("1.20.4", GameVersionDisplayType.Release),
            CreateItem("1.20.4", GameVersionDisplayType.Release),
            CreateItem("24w14a", GameVersionDisplayType.Snapshot)
        ]);

        Assert.Equal(["1.20.4", "24w14a"], candidates);
    }

    [Fact]
    public void CanOpenWizard_RequiresSelectedRoot()
    {
        Assert.False(DownloadMinecraftPagePresentation.CanOpenWizard(string.Empty));
        Assert.True(DownloadMinecraftPagePresentation.CanOpenWizard("C:\\Games\\.minecraft"));
    }

    [Fact]
    public void TryCreateWizardContext_ReturnsNullWithoutRoot()
    {
        Assert.Null(DownloadMinecraftPagePresentation.TryCreateWizardContext(null, "1.20.6"));

        var context = DownloadMinecraftPagePresentation.TryCreateWizardContext("C:\\Games\\.minecraft", "1.20.6");

        Assert.Equal(new DownloadMinecraftWizardContext("C:\\Games\\.minecraft", "1.20.6"), context);
    }

    [Fact]
    public void GetBuiltInIconResourcePath_ReturnsSnapshotIcon()
    {
        var path = DownloadMinecraftPagePresentation.GetBuiltInIconResourcePath(GameVersionDisplayType.Snapshot);

        Assert.Equal("/Assets/VersionIcons/snapshot.png", path);
    }

    private static ManifestVersionListItem CreateItem(string id, GameVersionDisplayType displayType)
    {
        return new ManifestVersionListItem
        {
            Id = id,
            DisplayType = displayType,
            DisplayTypeText = displayType.ToString(),
            LocalReleaseTimeText = "2026-06-02 00:00:00",
            Description = id,
            Source = new VersionEntry
            {
                Id = id,
                Type = "release",
                Url = "https://example.com",
                ReleaseTime = DateTimeOffset.UtcNow,
                Time = DateTimeOffset.UtcNow
            }
        };
    }
}
