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
using LMCCore.Game.Download.Vanilla;
using LMCCore.Game.Download.Installation.Caching;
using LMCCore.Game.Launching.Steps.Resources;

namespace LMCCore.Game.Download.Installation.Vanilla;

internal sealed class VanillaClientTaskContributor : IInstallationSubTaskContributor
{
    public void AddTasks(DownloadInstallationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var name = $"下载客户端文件 ({context.Request.VersionId})";
        var task = context.ParentTask.CreateSubTask(
            name,
            10,
            async (cancellationToken, _, progress) =>
            {
                var versionInfo = context.GetRequiredVersionInfo();
                if (versionInfo.Downloads == null ||
                    !versionInfo.Downloads.TryGetValue("client", out var clientDownload) ||
                    string.IsNullOrWhiteSpace(clientDownload.Url))
                {
                    progress.Report(100);
                    return null;
                }

                context.CacheManager.EnsureCacheDirectoryExists();
                var clientPath = context.CacheManager.CachedClientJarPath;
                var fileCheck = GameLaunchFileIntegrityHelper.CheckFile(
                    clientPath,
                    clientDownload.Sha1,
                    clientDownload.Size,
                    cancellationToken);
                if (fileCheck is { Exists: true, IsValid: true })
                {
                    progress.Report(100);
                    return clientPath;
                }

                await GameInstallationLocalReuseHelper.TryPopulateClientJarFromKnownVersionsAsync(
                    context.Request.RootPath,
                    clientPath,
                    clientDownload.Sha1,
                    clientDownload.Size,
                    cancellationToken);
                fileCheck = GameLaunchFileIntegrityHelper.CheckFile(
                    clientPath,
                    clientDownload.Sha1,
                    clientDownload.Size,
                    cancellationToken);
                if (fileCheck is { Exists: true, IsValid: true })
                {
                    progress.Report(100);
                    return clientPath;
                }

                await VanillaGameSubTaskFactory.DownloadSingleFileAsync(
                    context.DownloadSourceManager.TransformUrl(clientDownload.Url) ?? clientDownload.Url,
                    clientPath,
                    clientDownload.Sha1,
                    clientDownload.Size,
                    cancellationToken,
                    progress);

                return clientPath;
            },
            dependencies: [context.Tasks.VersionInfoTask!],
            translationKey: "Pages.TaskPage.Tasks.GameInstall.Vanilla.DownloadClientFile");

        context.Tasks.SetClientTask(task);
    }
}
