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
using LMCUI.Pages.DownloadMinecraftPage.DownloadMinecraft;
using LMCCore.Game.Download.Model;
using LMCCore.Game.Model.Loaders;
using LMCCore.Game.Download.Model.Vanilla;
using LMCCore.Game.Versioning;
using LMCUI.Pages.DownloadMinecraftPage;

namespace LineLauncherCs.Tests;

public class DownloadMinecraftWizardSupportTests
{
    [Fact]
    public void BuildManifestState_SortsVersionsAndFindsLatestEntries()
    {
        var manifest = new VersionManifestInfo
        {
            Latest = new LatestVersion
            {
                Release = "1.20.6",
                Snapshot = "24w18a"
            },
            Versions =
            [
                CreateVersion("1.20.5", "release", DateTimeOffset.Parse("2024-04-23T00:00:00Z")),
                CreateVersion("24w18a", "snapshot", DateTimeOffset.Parse("2024-04-25T00:00:00Z")),
                CreateVersion("1.20.6", "release", DateTimeOffset.Parse("2024-04-26T00:00:00Z"))
            ]
        };

        var state = DownloadMinecraftPagePresentation.BuildManifestState(manifest, type => type.ToString());

        Assert.Equal(["1.20.6", "24w18a", "1.20.5"], state.Versions.Select(item => item.Id));
        Assert.Equal("1.20.6", state.LatestRelease!.Id);
        Assert.Equal("24w18a", state.LatestSnapshot!.Id);
    }

    [Fact]
    public async Task LoadCatalogAsync_ParsesAvailableVersions()
    {
        var client = new FakeCatalogClient
        {
            FabricJson = """
                {
                  "game":[{"version":"1.20.6"}],
                  "loader":[{"version":"0.15.11"},{"version":"0.15.10"}]
                }
                """,
            ForgeJson = """
                [
                  {"version":"47.2.0"},
                  {"version":"47.1.0"}
                ]
                """,
            OptiFineJson = """
                [
                  {"patch":"HD_U_I6"},
                  {"patch":"HD_U_I5"}
                ]
                """
        };

        var result = await DownloadMinecraftWizardSupport.LoadCatalogAsync("1.20.6", CancellationToken.None, client);

        Assert.Null(result.Exception);
        Assert.Equal(["0.15.11", "0.15.10"], result.Catalog.FabricVersions);
        Assert.Equal(["47.2.0", "47.1.0"], result.Catalog.ForgeVersions);
        Assert.Equal(["HD_U_I5", "HD_U_I6"], result.Catalog.OptiFineVersions);
    }

    [Fact]
    public async Task LoadCatalogAsync_ReturnsEmptyCatalogWhenClientFails()
    {
        var result = await DownloadMinecraftWizardSupport.LoadCatalogAsync(
            "1.20.6",
            CancellationToken.None,
            new FakeCatalogClient { Exception = new InvalidOperationException("boom") });

        Assert.NotNull(result.Exception);
        Assert.Empty(result.Catalog.FabricVersions);
        Assert.Empty(result.Catalog.ForgeVersions);
        Assert.Empty(result.Catalog.OptiFineVersions);
    }

    [Fact]
    public void BuildLoaderSelectionState_ProducesMutualExclusionAndWarning()
    {
        var state = DownloadMinecraftWizardSupport.BuildLoaderSelectionState(
            new DownloadMinecraftWizardContext(@"C:\Games\.minecraft", "1.20.6"),
            "0.15.11",
            "Do not install",
            "HD_U_I6",
            false,
            "Do not install",
            "warning");

        Assert.NotNull(state.Selection);
        Assert.False(state.IsForgeEnabled);
        Assert.True(state.IsFabricEnabled);
        Assert.True(state.IsValidationVisible);
        Assert.Equal("warning", state.ValidationMessage);
    }

    [Fact]
    public void ValidateVersionName_BlocksExistingFolderAndBuildsResult()
    {
        var context = new DownloadMinecraftSelectionContext(
            @"C:\Games\.minecraft",
            "1.20.6",
            "0.15.11",
            null,
            null);

        var invalid = DownloadMinecraftWizardSupport.ValidateVersionName(
            context,
            "demo",
            path => path.EndsWith(@"\versions\demo", StringComparison.OrdinalIgnoreCase));
        var valid = DownloadMinecraftWizardSupport.ValidateVersionName(context, "demo-2", _ => false);

        Assert.False(invalid.IsValid);
        Assert.Equal("Pages.DownloadMinecraftPage.Wizard.Steps.VersionNameStep.Validation.AlreadyExists", invalid.ErrorMessage);
        Assert.True(valid.IsValid);
        Assert.Equal("demo-2", valid.Selection!.VersionName);
    }

    [Fact]
    public void CreateDownloadRequest_MapsSelectionToDownloadableGameVersion()
    {
        var selection = new DownloadableVersionSelection(
            "1.20.6",
            "1.20.6-Fabric_0.15.11",
            @"C:\Games\.minecraft",
            "0.15.11",
            "47.2.0",
            "I6");

        var request = DownloadMinecraftWizardSupport.CreateDownloadRequest(selection);

        Assert.Equal(selection.SelectedRootPath, request.RootPath);
        Assert.Equal(selection.ManifestVersionId, request.VersionId);
        Assert.Equal(selection.VersionName, request.VersionName);
        Assert.Equal("I6", request.OptiFine);
        Assert.Equal(2, request.Loaders.Length);
        Assert.Contains(request.Loaders, loader => loader.Type == ModLoaderType.Fabric && loader.VersionId == "0.15.11");
        Assert.Contains(request.Loaders, loader => loader.Type == ModLoaderType.Forge && loader.VersionId == "47.2.0");
    }

    [Theory]
    [InlineData(true, false, true)]
    [InlineData(true, true, false)]
    [InlineData(false, false, false)]
    public void ShouldCancelDialogClose_OnlyBlocksUserCloseWhileBusy(
        bool isDialogBusy,
        bool allowProgrammaticClose,
        bool expected)
    {
        var result = DownloadMinecraftWizardSupport.ShouldCancelDialogClose(isDialogBusy, allowProgrammaticClose);

        Assert.Equal(expected, result);
    }

    private static VersionEntry CreateVersion(string id, string type, DateTimeOffset releaseTime)
    {
        return new VersionEntry
        {
            Id = id,
            Type = type,
            Url = "https://example.com",
            Time = releaseTime,
            ReleaseTime = releaseTime
        };
    }

    private sealed class FakeCatalogClient : IDownloadMinecraftCatalogClient
    {
        public Exception? Exception { get; init; }
        public string FabricJson { get; init; } = """{"game":[],"loader":[]}""";
        public string? ForgeJson { get; init; } = "[]";
        public string? OptiFineJson { get; init; } = "[]";

        public Task<string> GetFabricVersionsJsonAsync(CancellationToken cancellationToken)
        {
            if (Exception != null)
            {
                throw Exception;
            }

            return Task.FromResult(FabricJson);
        }

        public Task<string?> GetForgeVersionsJsonAsync(string mcVersion, CancellationToken cancellationToken)
        {
            if (Exception != null)
            {
                throw Exception;
            }

            return Task.FromResult(ForgeJson);
        }

        public Task<string?> GetOptiFineVersionsJsonAsync(string mcVersion, CancellationToken cancellationToken)
        {
            if (Exception != null)
            {
                throw Exception;
            }

            return Task.FromResult(OptiFineJson);
        }
    }
}
