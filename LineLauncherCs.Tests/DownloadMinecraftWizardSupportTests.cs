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
using LMCCore.Game.Versioning.Discovery;
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
                  {"type":"HD_U","patch":"I6"},
                  {"type":"HD_U","patch":"I5"}
                ]
                """
        };

        var result = await DownloadMinecraftWizardSupport.LoadCatalogAsync("1.20.6", CancellationToken.None, client);

        Assert.Null(result.Exception);
        Assert.Equal(["0.15.11", "0.15.10"], result.Catalog.FabricVersions);
        Assert.Equal(["47.2.0", "47.1.0"], result.Catalog.ForgeVersions.Select(version => version.VersionId));
        Assert.Equal("jar", result.Catalog.ForgeVersions[0].InstallerFormat);
        Assert.Equal(["HD_U_I5", "HD_U_I6"], result.Catalog.OptiFineVersions);
    }

    [Theory]
    [InlineData("1.8", "1.8.0")]
    [InlineData("1.9", "1.9.0")]
    [InlineData("1.20.6", "1.20.6")]
    public void NormalizeRequestVersion_HandlesBmclLegacySpecialCases(string version, string expected)
    {
        var result = OptiFineCatalogVersionSupport.NormalizeRequestVersion(version);

        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("HD_U_I6", "HD_U", "I6")]
    [InlineData("I6", "HD_U", "I6")]
    public void TryParseSelectedVersion_PreservesTypeInformation(string version, string expectedType, string expectedPatch)
    {
        var parsed = OptiFineCatalogVersionSupport.TryParseSelectedVersion(version, out var type, out var patch);

        Assert.True(parsed);
        Assert.Equal(expectedType, type);
        Assert.Equal(expectedPatch, patch);
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
            new DownloadMinecraftWizardContext(@"C:\Games\.minecraft", "1.20.6", GameVersionDisplayType.Release),
            "0.15.11",
            null,
            "HD_U_I6",
            false,
            "Do not install",
            "warning");

        Assert.NotNull(state.Selection);
        Assert.False(state.IsForgeEnabled);
        Assert.False(state.IsFabricEnabled);
        Assert.True(state.IsValidationVisible);
        Assert.Equal("warning", state.ValidationMessage);
    }

    [Fact]
    public void BuildLoaderSelectionState_BlocksFabricAndOptiFineCombinationOutsideSupportedReleaseRange()
    {
        var state = DownloadMinecraftWizardSupport.BuildLoaderSelectionState(
            new DownloadMinecraftWizardContext(@"C:\Games\.minecraft", "1.13.2", GameVersionDisplayType.Release),
            "0.15.11",
            null,
            "HD_U_I6",
            false,
            "Do not install",
            "warning");

        Assert.False(state.CanContinue);
        Assert.False(state.IsOptiFineEnabled);
        Assert.True(state.IsValidationVisible);
    }

    [Fact]
    public void BuildLoaderSelectionState_DoesNotWarnForCompatibleFabricAndOptiFineCombination()
    {
        var state = DownloadMinecraftWizardSupport.BuildLoaderSelectionState(
            new DownloadMinecraftWizardContext(@"C:\Games\.minecraft", "1.20.4", GameVersionDisplayType.Release),
            "0.15.11",
            null,
            "HD_U_I6",
            false,
            "Do not install",
            "warning");

        Assert.True(state.CanContinue);
        Assert.False(state.IsValidationVisible);
        Assert.Equal(string.Empty, state.ValidationMessage);
    }

    [Fact]
    public void BuildLoaderSelectionState_IgnoresFabricAndOptiFineRestrictionForSnapshots()
    {
        var state = DownloadMinecraftWizardSupport.BuildLoaderSelectionState(
            new DownloadMinecraftWizardContext(@"C:\Games\.minecraft", "24w18a", GameVersionDisplayType.Snapshot),
            "0.15.11",
            null,
            "HD_U_I6",
            false,
            "Do not install",
            "warning");

        Assert.True(state.CanContinue);
        Assert.True(state.IsFabricEnabled);
        Assert.True(state.IsOptiFineEnabled);
        Assert.False(state.IsValidationVisible);
    }

    [Fact]
    public void ValidateVersionName_BlocksExistingFolderAndBuildsResult()
    {
        var context = new DownloadMinecraftSelectionContext(
            @"C:\Games\.minecraft",
            "1.20.6",
            GameVersionDisplayType.Release,
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
    public void CreatePreviousContext_PreservesOriginalDisplayType()
    {
        var context = new DownloadMinecraftSelectionContext(
            @"C:\Games\.minecraft",
            "3D Shareware v1.34",
            GameVersionDisplayType.AprilFools,
            "0.15.11",
            null,
            "HD_U_I6");

        var previous = DownloadMinecraftWizardSupport.CreatePreviousContext(context);

        Assert.Equal(GameVersionDisplayType.AprilFools, previous.DisplayType);
    }

    [Fact]
    public void CreateDownloadRequest_MapsSelectionToDownloadableGameVersion()
    {
        var selection = new DownloadableVersionSelection(
            "1.20.6",
            "1.20.6-Fabric_0.15.11",
            @"C:\Games\.minecraft",
            "0.15.11",
            new ForgeVersionCatalogEntry("47.2.0", "latest", "jar"),
            "HD_U_I6");

        var request = DownloadMinecraftWizardSupport.CreateDownloadRequest(selection);

        Assert.Equal(selection.SelectedRootPath, request.RootPath);
        Assert.Equal(selection.ManifestVersionId, request.VersionId);
        Assert.Equal(selection.VersionName, request.VersionName);
        Assert.Equal("HD_U_I6", request.OptiFine);
        Assert.Equal(2, request.Loaders.Length);
        Assert.Contains(request.Loaders, loader => loader.Type == ModLoaderType.Fabric && loader.VersionId == "0.15.11");
        Assert.Contains(request.Loaders, loader =>
            loader.Type == ModLoaderType.Forge &&
            loader.VersionId == "47.2.0" &&
            loader.Metadata != null &&
            loader.Metadata.TryGetValue("branch", out var branch) &&
            branch == "latest" &&
            loader.Metadata.TryGetValue("installerFormat", out var format) &&
            format == "jar");
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
