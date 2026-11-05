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

using LMC;
using LMC.Basic.Configs;
using LMCCore.Game.Model;
using LMCCore.Game.Model.Configuration;
using LMCCore.Game.Modpacks;
using LMCCore.Game.Versioning;

namespace LMCCore.Game.Download.Installation.Finalization;

public class GameInstallationFinalizer
{
    public async virtual Task FinalizeAsync(DownloadInstallationContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.Request.Modpack != null)
        {
            var content = Path.Combine(context.CacheManager.CacheDirectory, "modpack", "content");
            var requiredBytes = Directory.EnumerateFiles(content, "*", SearchOption.AllDirectories)
                .Append(context.CacheManager.CachedVersionJsonPath).Append(context.CacheManager.CachedClientJarPath)
                .Where(File.Exists).Sum(file => new FileInfo(file).Length);
            ModpackPaths.EnsureSpace(context.Request.RootPath, requiredBytes);
            await NewVersionPublisher.PublishAsync(context.Request.RootPath, context.Request.VersionName,
                (stagingRoot, token) => FinalizeCoreAsync(context, stagingRoot, token),
                () =>
                {
                    if (Current.Config.DefaultVersionConfigSourceForNewInstalls == NewVersionConfigSource.LMCDataDirectory)
                        WriteConfig(context, context.Request.RootPath);
                }, cancellationToken).ConfigureAwait(false);
            return;
        }

        await FinalizeCoreAsync(context, context.Request.RootPath, cancellationToken).ConfigureAwait(false);
    }

    private static async Task FinalizeCoreAsync(
        DownloadInstallationContext context, string outputRoot, CancellationToken cancellationToken)
    {
        await context.CacheManager.CopyCacheToVersionDirectoryAsync(
            outputRoot,
            context.Request.VersionName,
            cancellationToken);

        var optiFineState = context.RuntimeState.OptiFineInstallation;
        if (optiFineState is { InstallAsStandaloneMod: true, StandaloneModFileName: not null })
        {
            await context.CacheManager.CopyCachedFileToVersionDirectoryAsync(
                optiFineState.CachedInstallerJarPath,
                outputRoot,
                context.Request.VersionName,
                Path.Combine("mods", optiFineState.StandaloneModFileName),
                required: true,
                cancellationToken);
        }

        if (context.Request.FabricApi is { } api)
        {
            await context.CacheManager.CopyCachedFileToVersionDirectoryAsync(
                Path.Combine(context.CacheManager.CacheDirectory, "fabric-api.jar"),
                outputRoot, context.Request.VersionName,
                Path.Combine("mods", api.FileName), required: true, cancellationToken);
        }

        if (context.Request.Modpack != null)
        {
            var contentDirectory = Path.Combine(context.CacheManager.CacheDirectory, "modpack", "content");
            foreach (var file in Directory.EnumerateFiles(contentDirectory, "*", SearchOption.AllDirectories))
            {
                cancellationToken.ThrowIfCancellationRequested();
                await context.CacheManager.CopyCachedFileToVersionDirectoryAsync(file, outputRoot,
                    context.Request.VersionName, Path.GetRelativePath(contentDirectory, file), required: true,
                    cancellationToken).ConfigureAwait(false);
            }
        }

        if (context.Request.Modpack == null
            || Current.Config.DefaultVersionConfigSourceForNewInstalls != NewVersionConfigSource.LMCDataDirectory)
            WriteConfig(context, outputRoot);
    }

    private static void WriteConfig(DownloadInstallationContext context, string outputRoot)
    {
        var path = Path.Combine(outputRoot, "versions", context.Request.VersionName);
        var entry = new LocalGameVersionEntry
        {
            ClientVersionId = context.Request.VersionId,
            JarPath = Path.Combine(path, $"{context.Request.VersionName}.jar"),
            JsonPath = Path.Combine(path, $"{context.Request.VersionName}.json"),
            Status = VersionStatus.Valid,
            VersionName = context.Request.VersionName,
            RootPath = context.Request.RootPath,
            VersionDirectory = path,
            VersionInfo = context.GetRequiredVersionInfo()
        };
        
        new VersionConfigManager().WriteIndependentConfig(entry, new GameVersionConfig
        {
            Game = new GameVersionGameConfig
            {
                ModLoaders = context.Request.Loaders,
                OptiFine = context.Request.OptiFine,
                VersionId = context.Request.VersionId
            }
        }, Current.Config.DefaultVersionConfigSourceForNewInstalls);
    }
}
