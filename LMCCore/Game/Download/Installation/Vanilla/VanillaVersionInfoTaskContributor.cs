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

namespace LMCCore.Game.Download.Installation.Vanilla;

internal sealed class VanillaVersionInfoTaskContributor : IInstallationSubTaskContributor
{
    public void AddTasks(DownloadInstallationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var task = context.ParentTask.CreateSubTask(
            $"Get version info ({context.Request.VersionId})",
            0,
            async (cancellationToken, _, progress) =>
            {
                var baseVersionJson = await context.DownloadManager.ResolveVersionJsonAsync(
                    context.Request.VersionId,
                    cancellationToken);
                progress.Report(30);

                var resolvedVersionJson = await context.ApplyVersionJsonModifiersAsync(
                    baseVersionJson,
                    cancellationToken);
                var versionInfo = await context.PersistResolvedVersionJsonAsync(
                    resolvedVersionJson,
                    cancellationToken);
                progress.Report(100);
                return versionInfo;
            },
            dependencies: context.Tasks.GetVersionJsonDependencyTasks(),
            translationKey: "Pages.TaskPage.Tasks.GameInstall.Vanilla.GetVersionInfo");

        context.Tasks.SetVersionInfoTask(task);
    }
}
