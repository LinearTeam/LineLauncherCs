// Copyright 2025-2026 LinearTeam
// 
//    Licensed under the Apache License, Version 2.0 (the "License");
//    you may not use this file except in compliance with the License.
//    You may obtain a copy of the License at
// 
//        http://www.apache.org/licenses/LICENSE-2.0
// 
//    Unless required by applicable law or agreed to in writing, software
//    distributed under the License is distributed on an "AS IS" BASIS,
//    WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//    See the License for the specific language governing permissions and
//    limitations under the License.
using LMCCore.Game.Model;
using LMCCore.Game.Model.LocalVersion;
using LMCUI.Pages.VersionManagePage;

namespace LineLauncherCs.Tests;

public class VersionManagePagePresentationTests
{
    [Fact]
    public void BuildVersionRenderData_UsesFallbackTextAndResolvedIcon()
    {
        var version = CreateEntry(LocalGameVersionEntry.UnknownClientVersionId, VersionStatus.Valid);

        var renderData = VersionManagePagePresentation.BuildVersionRenderData(
            version,
            _ => "Release",
            "Unknown",
            (_, _) => (VersionIconKind.Asset, "/Assets/VersionIcons/release.png"));

        Assert.Equal("Release - Unknown", renderData.Description);
        Assert.Equal(VersionIconKind.Asset, renderData.IconKind);
        Assert.Equal("/Assets/VersionIcons/release.png", renderData.IconPath);
    }

    [Fact]
    public void GetDisplayType_ReturnsErrorForInvalidVersion()
    {
        var version = CreateEntry("1.20.4", VersionStatus.InvalidJson);

        var displayType = VersionManagePagePresentation.GetDisplayType(version);

        Assert.Equal(VersionDisplayType.Error, displayType);
    }

    [Fact]
    public void BuildRootListItems_UsesFolderNameAsHeader()
    {
        var roots = new[]
        {
            new ManagedGameRoot
            {
                RootPath = Path.Combine("C:\\Games", "Minecraft")
            }
        };

        var items = VersionManagePagePresentation.BuildRootListItems(roots);

        Assert.Single(items);
        Assert.Equal("Minecraft", items[0].Header);
        Assert.Equal(roots[0].RootPath, items[0].Description);
    }

    [Fact]
    public void GetSelectedRootIndex_PrefersMatchingRootPath()
    {
        var selectedRoot = new ManagedGameRoot
        {
            RootPath = Path.Combine("C:\\Games", "Second")
        };
        var items = VersionManagePagePresentation.BuildRootListItems(
        [
            new ManagedGameRoot { RootPath = Path.Combine("C:\\Games", "First") },
            selectedRoot
        ]);

        var index = VersionManagePagePresentation.GetSelectedRootIndex(items, selectedRoot);

        Assert.Equal(1, index);
    }

    [Fact]
    public void BuildInvalidVersionNotification_ReturnsSignatureAndItems()
    {
        var versions = new[]
        {
            CreateEntry("1.20.4", VersionStatus.Valid),
            CreateEntry("1.20.4", VersionStatus.InvalidJson, "broken")
        };

        var notification = VersionManagePagePresentation.BuildInvalidVersionNotification(
            versions,
            status => status.ToString());

        Assert.NotNull(notification);
        Assert.Equal("broken (InvalidJson)", notification!.Signature);
        Assert.Equal(["broken (InvalidJson)"], notification.Items);
    }

    [Fact]
    public void ResolveVersionIcon_PrefersExistingCustomFile()
    {
        using var scope = new TestFileSystemScope();
        var iconPath = scope.GetPath("icon.png");
        File.WriteAllText(iconPath, "icon");

        var result = VersionManagePagePresentation.ResolveVersionIcon(VersionDisplayType.Release, iconPath);

        Assert.Equal(VersionIconKind.File, result.IconKind);
        Assert.Equal(iconPath, result.IconPath);
    }

    [Fact]
    public void RefreshState_OnlyConsumesLatestDebouncedRequest()
    {
        var state = new VersionManagePageRefreshState();
        var firstToken = state.QueueExternalRefresh();
        var secondToken = state.QueueExternalRefresh();

        Assert.False(state.TryConsumeDebouncedRefresh(firstToken, true));
        Assert.True(state.TryConsumeDebouncedRefresh(secondToken, true));
    }

    [Fact]
    public void ShouldRefreshOnActivation_RespectsPendingFlagAndTimeThreshold()
    {
        var now = DateTime.UtcNow;

        Assert.True(VersionManagePagePresentation.ShouldRefreshOnActivation(true, true, now, now));
        Assert.True(VersionManagePagePresentation.ShouldRefreshOnActivation(true, false, now.AddSeconds(-3), now));
        Assert.False(VersionManagePagePresentation.ShouldRefreshOnActivation(false, true, now.AddSeconds(-3), now));
        Assert.False(VersionManagePagePresentation.ShouldRefreshOnActivation(true, false, now.AddSeconds(-1), now));
    }

    [Fact]
    public void GetBuiltInIconResourcePath_ReturnsExpectedSnapshotIcon()
    {
        var path = VersionManagePagePresentation.GetBuiltInIconResourcePath(VersionDisplayType.Snapshot);

        Assert.Equal("/Assets/VersionIcons/snapshot.png", path);
    }

    [Fact]
    public void GetBuiltInIconResourcePath_ReturnsEmptyForErrorDisplayType()
    {
        var path = VersionManagePagePresentation.GetBuiltInIconResourcePath(VersionDisplayType.Error);

        Assert.Equal(string.Empty, path);
    }

    private static LocalGameVersionEntry CreateEntry(string clientVersionId, VersionStatus status, string versionName = "version")
    {
        return new LocalGameVersionEntry
        {
            RootPath = "root",
            VersionName = versionName,
            VersionDirectory = "root\\version",
            ClientVersionId = clientVersionId,
            Status = status,
            VersionInfo = new LocalVersionInfo
            {
                Id = "version",
                MainClass = "main",
                Libraries = []
            }
        };
    }
}
