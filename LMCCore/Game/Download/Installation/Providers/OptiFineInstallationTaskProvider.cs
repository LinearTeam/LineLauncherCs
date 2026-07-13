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

using System.Diagnostics;
using LMC.Basic.Logging;
using LMCCore.Game.Download.Installation.Forge;
using LMCCore.Game.Download.Installation.OptiFine;
using LMCCore.Game.Download.Vanilla;
using LMCCore.Game.Launching.Steps.Resources;
using LMCCore.Game.Model.Loaders;
using LMCCore.Utils;

namespace LMCCore.Game.Download.Installation.Providers;

public sealed class OptiFineInstallationTaskProvider : IGameInstallationTaskProvider, IGameInstallationVersionJsonModifier
{
    private readonly Logger _logger = new("OptiFineInstallation");

    public DownloadInstallationComponent Component => DownloadInstallationComponent.OptiFine;

    public bool ShouldApply(DownloadInstallationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return !string.IsNullOrWhiteSpace(context.Request.OptiFine);
    }

    public void AddTasks(DownloadInstallationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var optiFineVersion = context.Request.OptiFine
                             ?? throw new InvalidOperationException("OptiFine version is missing.");
        var dependencies = GetTaskDependencies(context);
        var task = context.ParentTask.CreateSubTask(
            $"Install OptiFine ({optiFineVersion})",
            70,
            async (cancellationToken, _, progress) =>
            {
                var state = await InstallAsync(context, optiFineVersion, cancellationToken, progress);
                progress.Report(100);
                return state;
            },
            dependencies: dependencies,
            translationKey: "Pages.TaskPage.Tasks.GameInstall.OptiFine.Install");

        context.Tasks.SetOptiFineTask(task);
        context.Tasks.RegisterVersionJsonDependencyTask(task);
    }

    public Task<string> ModifyVersionJsonAsync(
        string versionJson,
        DownloadInstallationContext context,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(versionJson);
        ArgumentNullException.ThrowIfNull(context);

        var state = context.Tasks.OptiFineTask?.Result
                    ?? throw new InvalidOperationException("OptiFine installation task has not completed.");
        if (state.InstallAsStandaloneMod)
        {
            return Task.FromResult(versionJson);
        }

        return Task.FromResult(OptiFineVersionJsonMutator.Apply(versionJson, state));
    }

    private IReadOnlyList<LMCCore.Tasks.Model.SubTaskBase> GetTaskDependencies(DownloadInstallationContext context)
    {
        var dependencies = new List<LMCCore.Tasks.Model.SubTaskBase>();
        if (context.Tasks.ForgeProcessorsTask != null)
        {
            dependencies.Add(context.Tasks.ForgeProcessorsTask);
        }

        return dependencies;
    }

    async private Task<OptiFineInstallationRuntimeState> InstallAsync(
        DownloadInstallationContext context,
        string optiFineVersion,
        CancellationToken cancellationToken,
        IProgress<int> progress)
    {
        if (!OptiFineVersionResolver.TryParseSelectedVersion(optiFineVersion, out var type, out var patch))
        {
            throw new InvalidOperationException($"Failed to parse OptiFine version '{optiFineVersion}'.");
        }

        var minecraftVersion = context.Request.VersionId;
        var installerFileName = $"optifine-{minecraftVersion}-{type}-{patch}-installer.jar";
        var cachedInstallerJarPath = Path.Combine(context.CacheManager.CacheDirectory, installerFileName);
        var installerCoordinate = OptiFineVersionResolver.BuildInstallerLibraryCoordinate(minecraftVersion, type, patch);
        var libraryCoordinate = OptiFineVersionResolver.BuildLibraryCoordinate(minecraftVersion, type, patch);
        var installerLibraryPath = OptiFineLibraryPathHelper.GetAbsoluteLibraryPath(context.Request.RootPath, installerCoordinate);
        var libraryPath = OptiFineLibraryPathHelper.GetAbsoluteLibraryPath(context.Request.RootPath, libraryCoordinate);
        var installerFileNameForMods = $"OptiFine-{minecraftVersion}-{type}-{patch}-installer.jar";
        var state = new OptiFineInstallationRuntimeState
        {
            MinecraftVersion = minecraftVersion,
            Patch = patch,
            Type = type,
            InstallerDownloadUrl = OptiFineVersionResolver.BuildInstallerUrl(minecraftVersion, type, patch),
            CachedInstallerJarPath = cachedInstallerJarPath,
            CachedMinecraftJarPath = context.CacheManager.CachedClientJarPath,
            InstallerLibraryCoordinate = installerCoordinate,
            InstallerLibraryPath = installerLibraryPath,
            LibraryCoordinate = libraryCoordinate,
            LibraryPath = libraryPath,
            StandaloneModFileName = installerFileNameForMods
        };

        context.CacheManager.EnsureCacheDirectoryExists();

        await VanillaGameSubTaskFactory.DownloadSingleFileAsync(
            context.DownloadSourceManager.TransformUrl(state.InstallerDownloadUrl) ?? state.InstallerDownloadUrl,
            cachedInstallerJarPath,
            null,
            null,
            cancellationToken);
        progress.Report(20);

        if (ShouldInstallInstallerAsStandaloneMod(context))
        {
            MarkInstallerForStandaloneModInstallation(state);
            progress.Report(100);
            _logger.Info($"Queued OptiFine installer for version-local mods finalization: mods\\{state.StandaloneModFileName}");
            context.RuntimeState.OptiFineInstallation = state;
            return state;
        }

        await PrepareVanillaClientJarAsync(context, cancellationToken);
        progress.Report(40);

        OptiFineInstallerArchiveEditor.CopyInstallerWithoutModsToml(cachedInstallerJarPath, installerLibraryPath);
        state.HasPatcher = OptiFineInstallerArchiveEditor.HasPatcher(cachedInstallerJarPath);

        if (state.HasPatcher)
        {
            await ExecutePatcherAsync(context, state, cancellationToken);
        }
        else
        {
            OptiFineInstallerArchiveEditor.CopyInstallerWithoutModsToml(cachedInstallerJarPath, libraryPath);
        }

        progress.Report(70);
        RemoveModsTomlIfPresent(libraryPath);
        await ConfigureLaunchWrapperAsync(context, state, cancellationToken);
        progress.Report(90);

        context.RuntimeState.OptiFineInstallation = state;
        return state;
    }

    private static bool ShouldInstallInstallerAsStandaloneMod(DownloadInstallationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return context.HasLoader(ModLoaderType.Fabric) || context.HasLoader(ModLoaderType.Forge);
    }

    private static void MarkInstallerForStandaloneModInstallation(OptiFineInstallationRuntimeState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        state.InstallAsStandaloneMod = true;
    }

    async private Task PrepareVanillaClientJarAsync(DownloadInstallationContext context, CancellationToken cancellationToken)
    {
        var versionJson = await context.DownloadManager.ResolveVersionJsonAsync(context.Request.VersionId, cancellationToken);
        var versionInfo = context.DownloadManager.ParseVersionJson(versionJson)
                          ?? throw new InvalidOperationException($"Failed to parse base version json for '{context.Request.VersionId}'.");

        if (versionInfo.Downloads == null ||
            !versionInfo.Downloads.TryGetValue("client", out var clientDownload) ||
            string.IsNullOrWhiteSpace(clientDownload.Url))
        {
            throw new InvalidOperationException($"Version '{context.Request.VersionId}' does not expose a client download.");
        }

        var check = GameLaunchFileIntegrityHelper.CheckFile(
            context.CacheManager.CachedClientJarPath,
            clientDownload.Sha1,
            clientDownload.Size,
            cancellationToken);
        if (check is { Exists: true, IsValid: true })
        {
            return;
        }

        await VanillaGameSubTaskFactory.DownloadSingleFileAsync(
            context.DownloadSourceManager.TransformUrl(clientDownload.Url) ?? clientDownload.Url,
            context.CacheManager.CachedClientJarPath,
            clientDownload.Sha1,
            clientDownload.Size,
            cancellationToken);
    }

    async private Task ExecutePatcherAsync(
        DownloadInstallationContext context,
        OptiFineInstallationRuntimeState state,
        CancellationToken cancellationToken)
    {
        var java = await ForgeJavaSelector.SelectJavaAsync(cancellationToken);
        var javaPath = ForgeJavaSelector.GetJavaExecutablePath(java);

        Directory.CreateDirectory(Path.GetDirectoryName(state.LibraryPath)!);

        var startInfo = new ProcessStartInfo
        {
            FileName = javaPath,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        startInfo.ArgumentList.Add("-cp");
        startInfo.ArgumentList.Add(state.CachedInstallerJarPath);
        startInfo.ArgumentList.Add("optifine.Patcher");
        startInfo.ArgumentList.Add(state.CachedMinecraftJarPath);
        startInfo.ArgumentList.Add(state.CachedInstallerJarPath);
        startInfo.ArgumentList.Add(state.LibraryPath);

        using var process = new Process { StartInfo = startInfo };
        process.Start();
        var stdoutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var stderrTask = process.StandardError.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken);

        var stdout = await stdoutTask;
        var stderr = await stderrTask;
        if (!string.IsNullOrWhiteSpace(stdout))
        {
            _logger.Debug(stdout);
        }

        if (!string.IsNullOrWhiteSpace(stderr))
        {
            _logger.Warn(stderr);
        }

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException($"OptiFine patcher exited with code {process.ExitCode}.");
        }
    }

    async private Task ConfigureLaunchWrapperAsync(
        DownloadInstallationContext context,
        OptiFineInstallationRuntimeState state,
        CancellationToken cancellationToken)
    {
        if (OptiFineInstallerArchiveEditor.HasEmbeddedLaunchWrapperJar(state.CachedInstallerJarPath))
        {
            state.LaunchWrapperCoordinate = "optifine:launchwrapper:2.0";
            state.LaunchWrapperPath = OptiFineLibraryPathHelper.GetAbsoluteLibraryPath(context.Request.RootPath, state.LaunchWrapperCoordinate);
            OptiFineInstallerArchiveEditor.ExtractEmbeddedLibrary(
                state.CachedInstallerJarPath,
                "launchwrapper-2.0.jar",
                state.LaunchWrapperPath);
            state.HasEmbeddedLaunchWrapper = true;
            return;
        }

        var wrapperOfVersion = OptiFineInstallerArchiveEditor.ResolveEmbeddedLaunchWrapperVersion(state.CachedInstallerJarPath);
        if (!string.IsNullOrWhiteSpace(wrapperOfVersion) &&
            OptiFineInstallerArchiveEditor.HasEmbeddedLaunchWrapperOfJar(state.CachedInstallerJarPath, wrapperOfVersion))
        {
            state.LaunchWrapperCoordinate = $"optifine:launchwrapper-of:{wrapperOfVersion}";
            state.LaunchWrapperPath = OptiFineLibraryPathHelper.GetAbsoluteLibraryPath(context.Request.RootPath, state.LaunchWrapperCoordinate);
            OptiFineInstallerArchiveEditor.ExtractEmbeddedLibrary(
                state.CachedInstallerJarPath,
                $"launchwrapper-of-{wrapperOfVersion}.jar",
                state.LaunchWrapperPath);
            state.HasEmbeddedLaunchWrapper = true;
            return;
        }

        state.LaunchWrapperCoordinate = "net.minecraft:launchwrapper:1.12";
        state.LaunchWrapperPath = OptiFineLibraryPathHelper.GetAbsoluteLibraryPath(context.Request.RootPath, state.LaunchWrapperCoordinate);
        var relativePath = VanillaGameDownloader.TryBuildMavenRelativePath(state.LaunchWrapperCoordinate, out var resolvedRelativePath)
            ? resolvedRelativePath
            : throw new InvalidOperationException("Failed to resolve launchwrapper library path.");
        var launchWrapperUrl = $"https://libraries.minecraft.net/{relativePath}";
        await VanillaGameSubTaskFactory.DownloadSingleFileAsync(
            context.DownloadSourceManager.TransformUrl(launchWrapperUrl) ?? launchWrapperUrl,
            state.LaunchWrapperPath,
            null,
            null,
            cancellationToken);
    }

    private static void RemoveModsTomlIfPresent(string jarPath)
    {
        if (!File.Exists(jarPath))
        {
            return;
        }

        var tempPath = Path.Combine(Path.GetDirectoryName(jarPath)!, Path.GetRandomFileName());
        OptiFineInstallerArchiveEditor.CopyInstallerWithoutModsToml(jarPath, tempPath);
        File.Delete(jarPath);
        File.Move(tempPath, jarPath);
    }
}
