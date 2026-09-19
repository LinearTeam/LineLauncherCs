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
using System.Text.Json.Nodes;
using LMC.Basic.Logging;
using LMCCore.Game.Download.Installation.Forge;
using LMCCore.Game.Download.Vanilla;
using LMCCore.Game.Model.Loaders;
using LMCCore.Game.Model.LocalVersion;
using LMCCore.Utils;

namespace LMCCore.Game.Download.Installation.Providers;

public sealed class ForgeInstallationTaskProvider : ModLoaderInstallationTaskProvider, IGameInstallationVersionJsonModifier
{
    private readonly Logger _logger = new("ForgeInstallation");

    public override ModLoaderType LoaderType => ModLoaderType.Forge;

    public override void AddTasks(DownloadInstallationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var loader = context.GetRequiredLoader(LoaderType);
        var installerTask = context.ParentTask.CreateSubTask(
            $"Download Forge installer ({loader.VersionId})",
            -20,
            async (cancellationToken, _, progress) =>
            {
                var forgeState = await DownloadInstallerAsync(context, loader, cancellationToken);
                progress.Report(100);
                return forgeState;
            },
            translationKey: "Pages.TaskPage.Tasks.GameInstall.Forge.DownloadInstaller");

        context.Tasks.SetForgeInstallerTask(installerTask);
        context.Tasks.RegisterVersionJsonDependencyTask(installerTask);
    }

    public Task<string> ModifyVersionJsonAsync(
        string versionJson,
        DownloadInstallationContext context,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(versionJson);
        ArgumentNullException.ThrowIfNull(context);

        var forgeState = context.Tasks.ForgeInstallerTask?.Result
                         ?? throw new InvalidOperationException("Forge installer task has not completed.");
        if (string.IsNullOrWhiteSpace(forgeState.VersionJson))
        {
            throw new InvalidOperationException("Forge installer does not contain mergeable version metadata.");
        }

        var vanilla = JsonUtils.Parse(versionJson);
        var forge = JsonUtils.Parse(forgeState.VersionJson);
        return Task.FromResult(vanilla.Merge(forge, ["time", "releaseTime", "inheritsFrom", "id", "_comment"]).ToString());
    }

    async private Task<ForgeInstallationRuntimeState> DownloadInstallerAsync(
        DownloadInstallationContext context,
        ModLoader loader,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(loader);

        var installerUrl = ForgeVersionResolver.ResolveInstallerDownloadUrl(context.Request.VersionId, loader);
        var installerFileName = Path.GetFileName(new Uri(installerUrl).LocalPath);
        var installerJarPath = Path.Combine(context.CacheManager.CacheDirectory, installerFileName);
        var candidateUrls = context.DownloadSourceManager.GetUrlCandidates(installerUrl);

        context.CacheManager.EnsureCacheDirectoryExists();
        await InstallationFileDownloader.DownloadFileWithFallbackAsync(
            candidateUrls,
            installerJarPath,
            cancellationToken);

        var installProfileJson = ForgeInstallerArchiveReader.ReadRequiredTextEntry(installerJarPath, "install_profile.json");
        var versionJson = ForgeInstallerArchiveReader.ReadOptionalTextEntry(installerJarPath, "version.json");
        var installProfileObject = JsonNode.Parse(installProfileJson)?.AsObject()
                                   ?? throw new InvalidOperationException("Failed to parse Forge install_profile.json.");
        var installProfile = JsonUtils.Parse(installProfileJson).Get<ForgeInstallProfile>()
                             ?? throw new InvalidOperationException("Failed to deserialize Forge install_profile.json.");
        var isLegacyInstaller = ResolveInstallerKind(installProfile);
        if (isLegacyInstaller)
        {
            ExtractLegacyInstallerLibrary(context, installProfile, installerJarPath);
        }

        var mergeableVersionJson = ResolveMergeableVersionJson(versionJson, installProfile, isLegacyInstaller);

        var state = new ForgeInstallationRuntimeState
        {
            LoaderVersion = loader.VersionId,
            ArtifactVersion = ForgeVersionResolver.ResolveArtifactVersion(context.Request.VersionId, loader.VersionId),
            InstallerJarPath = installerJarPath,
            InstallProfileJson = installProfileJson,
            VersionJson = mergeableVersionJson,
            IsLegacyInstaller = isLegacyInstaller,
            InstallProfileObject = installProfileObject,
            VersionJsonObject = string.IsNullOrWhiteSpace(mergeableVersionJson)
                ? null
                : JsonNode.Parse(mergeableVersionJson)?.AsObject()
        };

        context.RuntimeState.ForgeInstallation = state;
        return state;
    }

    private static bool ResolveInstallerKind(ForgeInstallProfile installProfile)
    {
        ArgumentNullException.ThrowIfNull(installProfile);

        if (installProfile.Spec is not null)
        {
            return false;
        }

        if (installProfile is { Install: not null, VersionInfo: not null })
        {
            return true;
        }

        throw new InvalidOperationException(
            "Forge install_profile.json does not contain a supported installer shape. Expected either 'spec' for modern Forge or both 'install' and 'versionInfo' for legacy Forge.");
    }

    private static string ResolveMergeableVersionJson(
        string? modernVersionJson,
        ForgeInstallProfile installProfile,
        bool isLegacyInstaller)
    {
        if (!isLegacyInstaller)
        {
            return !string.IsNullOrWhiteSpace(modernVersionJson)
                ? modernVersionJson
                : throw new InvalidOperationException("Forge installer does not contain version.json.");
        }

        return installProfile.VersionInfo is not { } versionInfoElement ? throw new InvalidOperationException("Legacy Forge installer does not contain install_profile.versionInfo.") : versionInfoElement.GetRawText();

    }

    private static void ExtractLegacyInstallerLibrary(
        DownloadInstallationContext context,
        ForgeInstallProfile installProfile,
        string installerJarPath)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(installProfile);
        ArgumentException.ThrowIfNullOrWhiteSpace(installerJarPath);

        var extraction = ResolveLegacyInstallerExtraction(installProfile);
        if (extraction == null)
        {
            return;
        }

        var destinationPath = ForgeLibraryPathHelper.GetAbsoluteLibraryPath(
            context.Request.RootPath,
            extraction.LibraryCoordinate);
        ForgeInstallerArchiveReader.ExtractEntry(installerJarPath, extraction.EntryPath, destinationPath);
    }

    private static LegacyForgeInstallerExtraction? ResolveLegacyInstallerExtraction(ForgeInstallProfile installProfile)
    {
        ArgumentNullException.ThrowIfNull(installProfile.Install);

        return new LegacyForgeInstallerExtraction(installProfile.Install.FilePath ?? throw new NullReferenceException("'filePath' in 'install' of forge install profile is null."), 
            installProfile.Install.Path ?? throw new NullReferenceException("'path' in 'install' of forge install profile is null."));
    }

    private sealed record LegacyForgeInstallerExtraction(string EntryPath, string LibraryCoordinate);
}
