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

using LMCCore.Game.Launching.Steps.Resources;
using LMCCore.Game.Download.Installation.Caching;

namespace LMCCore.Game.Download.Installation.Fabric;

public sealed class FabricApiTaskContributor : IInstallationSubTaskContributor
{
    public void AddTasks(DownloadInstallationContext context)
    {
        if (context.Request.FabricApi is not { } api)
            return;

        if (Path.GetFileName(api.FileName) != api.FileName || api.FileName.Contains('\\')
            || !api.FileName.EndsWith(".jar", StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Invalid Fabric API file name.");

        var task = context.ParentTask.CreateSubTask(
            $"Download Fabric API ({api.Version})", 20,
            async (cancellationToken, _, progress) =>
            {
                context.CacheManager.EnsureCacheDirectoryExists();
                var path = Path.Combine(context.CacheManager.CacheDirectory, "fabric-api.jar");
                var reused = await GameInstallationLocalReuseHelper.TryPopulateFabricApiFromLocalModsAsync(
                    context.Request.RootPath, path, api.FileName, api.Sha1, api.Size, cancellationToken)
                    .ConfigureAwait(false);
                if (!reused)
                    await InstallationFileDownloader.DownloadFileWithFallbackAsync([api.Url], path, cancellationToken)
                        .ConfigureAwait(false);
                var result = GameLaunchFileIntegrityHelper.CheckFile(path, api.Sha1, api.Size, cancellationToken);
                if (!result.IsValid)
                    throw new InvalidDataException($"Fabric API validation failed: {result.Reason}");
                progress.Report(100);
                return path;
            }, translationKey: "Pages.TaskPage.Tasks.GameInstall.Fabric.DownloadApi");
        context.Tasks.Register("fabric-api", task);
    }
}
