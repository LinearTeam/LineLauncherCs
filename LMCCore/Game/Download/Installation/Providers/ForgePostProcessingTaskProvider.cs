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
using LMC.Basic.Logging;
using LMCCore.Game.Download.Installation.Forge;
using LMCCore.Game.Download.Vanilla;
using LMCCore.Game.Model.Loaders;
using LMCCore.Utils;

namespace LMCCore.Game.Download.Installation.Providers;

public sealed class ForgePostProcessingTaskProvider : ModLoaderInstallationTaskProvider
{
    private readonly Logger _logger = new("ForgeInstallation");
    private readonly ForgeProcessorExecutor _processorExecutor;

    public ForgePostProcessingTaskProvider()
    {
        _processorExecutor = new ForgeProcessorExecutor(_logger);
    }

    public override ModLoaderType LoaderType => ModLoaderType.Forge;

    public override void AddTasks(DownloadInstallationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var loader = context.GetRequiredLoader(LoaderType);
        var versionInfoTask = context.Tasks.VersionInfoTask
                              ?? throw new InvalidOperationException("Version info task must be created before Forge post-processing tasks.");
        var installerTask = context.Tasks.ForgeInstallerTask
                            ?? throw new InvalidOperationException("Forge installer task must be created before Forge post-processing tasks.");
        var clientTask = context.Tasks.ClientTask
                         ?? throw new InvalidOperationException("Client task must be created before Forge post-processing tasks.");
        var librariesTask = context.Tasks.LibrariesTask
                            ?? throw new InvalidOperationException("Libraries task must be created before Forge post-processing tasks.");

        var processorsTask = context.ParentTask.CreateSubTask(
            $"Execute Forge processors ({loader.VersionId})",
            75,
            async (cancellationToken, _, progress) =>
            {
                var forgeState = context.RuntimeState.ForgeInstallation
                                 ?? throw new InvalidOperationException("Forge installer state has not been initialized.");
                if (forgeState.IsLegacyInstaller)
                {
                    _logger.Info($"Skipping processor execution for legacy Forge installer '{loader.VersionId}'.");
                    progress.Report(100);
                    return true;
                }

                await DownloadAdditionalLibrariesAsync(context, forgeState, cancellationToken);
                await ExtractForgeLzmaFilesAsync(context, forgeState, cancellationToken);
                await _processorExecutor.ExecuteProcessorsAsync(context, forgeState, cancellationToken, progress);
                return true;
            },
            dependencies: [installerTask, versionInfoTask, clientTask, librariesTask],
            translationKey: "Pages.TaskPage.Tasks.GameInstall.Forge.RunProcessors");

        context.Tasks.SetForgeProcessorsTask(processorsTask);
    }

    async private Task DownloadAdditionalLibrariesAsync(
        DownloadInstallationContext context,
        ForgeInstallationRuntimeState forgeState,
        CancellationToken cancellationToken)
    {
        var installProfile = JsonUtils.Parse(forgeState.InstallProfileJson).Get<ForgeInstallProfile>()
                             ?? throw new InvalidOperationException("Failed to parse install_profile.json.");
        var libraries = installProfile.Libraries ?? [];
        if (libraries.Count == 0)
        {
            return;
        }

        var downloadFiles = libraries
            .Where(library => library.Downloads?.Artifact?.Url != null)
            .Select(library =>
            {
                var artifact = library.Downloads!.Artifact!;
                artifact.Path ??= NormalizeRelativeLibraryPath(library.Name);
                return artifact;
            })
            .ToList();

        if (downloadFiles.Count == 0)
        {
            return;
        }

        var executor = VanillaGameSubTaskFactory.CreateLibrariesExecutor(
            context.VanillaDownloader,
            downloadFiles,
            context.Request.RootPath);
        await executor(cancellationToken, new Dictionary<Tasks.Model.SubTaskBase, object>(), new Progress<int>());

        forgeState.AdditionalLibraries.Clear();
        forgeState.AdditionalLibraries.AddRange(downloadFiles);
    }

    private Task ExtractForgeLzmaFilesAsync(
        DownloadInstallationContext context,
        ForgeInstallationRuntimeState forgeState,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        ExtractIfExists(
            context,
            forgeState,
            "data/client.lzma",
            $"net.minecraftforge:forge:{forgeState.LoaderVersion}:clientdata@lzma");
        ExtractIfExists(
            context,
            forgeState,
            "data/server.lzma",
            $"net.minecraftforge:forge:{forgeState.LoaderVersion}:serverdata@lzma");

        return Task.CompletedTask;
    }

    private void ExtractIfExists(
        DownloadInstallationContext context,
        ForgeInstallationRuntimeState forgeState,
        string entryPath,
        string libraryCoordinate)
    {
        if (!ForgeInstallerArchiveReader.ContainsEntry(forgeState.InstallerJarPath, entryPath))
        {
            return;
        }

        var destinationPath = ForgeLibraryPathHelper.GetAbsoluteLibraryPath(context.Request.RootPath, libraryCoordinate);
        ForgeInstallerArchiveReader.ExtractEntry(forgeState.InstallerJarPath, entryPath, destinationPath);
    }

    private static string NormalizeRelativeLibraryPath(string libraryName)
    {
        var relativePath = ForgeLibraryPathHelper.GetLibPath(libraryName)
                           ?? throw new InvalidOperationException($"Failed to resolve Forge library path for '{libraryName}'.");
        return relativePath
            .Replace(".minecraft", string.Empty, StringComparison.OrdinalIgnoreCase)
            .TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            .Replace(Path.DirectorySeparatorChar, '/');
    }
}
