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

using LMCCore.Game.Modpacks;

namespace LMCCore.Game.Download.Installation.Providers;

public sealed class ModrinthModpackTaskProvider : IGameInstallationTaskProvider
{
    public DownloadInstallationComponent Component => DownloadInstallationComponent.Modpack;
    public bool ShouldApply(DownloadInstallationContext context) => context.Request.Modpack != null;

    public void AddTasks(DownloadInstallationContext context)
    {
        var installation = context.Request.Modpack!;
        var cachedArchive = Path.Combine(context.CacheManager.CacheDirectory, "modpack.zip");
        var contentDirectory = Path.Combine(context.CacheManager.CacheDirectory, "modpack", "content");
        var prepare = context.ParentTask.CreateSubTask(
            "Read Modrinth modpack", 5,
            async (token, _, progress) =>
            {
                var target = Path.Combine(context.Request.RootPath, "versions", context.Request.VersionName);
                if (Directory.Exists(target) || File.Exists(target))
                    throw new IOException("The target version already exists.");
                await ModrinthModpackInstaller.PrepareArchiveAsync(installation,
                    cachedArchive, contentDirectory, token).ConfigureAwait(false);
                progress.Report(100);
                return true;
            }, translationKey: "Pages.TaskPage.Tasks.GameInstall.Modpack.Prepare");
        context.Tasks.Register("modpack-prepare", prepare);

        var download = context.ParentTask.CreateSubTask(
            "Download modpack files", 30,
            async (token, _, progress) =>
            {
                await ModrinthModpackInstaller.DownloadFilesAsync(
                    installation, context.Request.RootPath, contentDirectory, progress, token)
                    .ConfigureAwait(false);
                return true;
            }, dependencies: [prepare],
            translationKey: "Pages.TaskPage.Tasks.GameInstall.Modpack.Download");
        context.Tasks.Register("modpack-download", download);

        var extract = context.ParentTask.CreateSubTask(
            "Apply modpack overrides", 40,
            async (token, _, progress) =>
            {
                await ModrinthModpackInstaller.ExtractOverridesAsync(
                    cachedArchive, contentDirectory, context.Request.VersionName, token).ConfigureAwait(false);
                progress.Report(100);
                return true;
            }, dependencies: [download],
            translationKey: "Pages.TaskPage.Tasks.GameInstall.Modpack.Overrides");
        context.Tasks.Register("modpack-overrides", extract);
    }
}
