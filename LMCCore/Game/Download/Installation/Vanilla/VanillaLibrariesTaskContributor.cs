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

namespace LMCCore.Game.Download.Installation.Vanilla;

internal sealed class VanillaLibrariesTaskContributor : IInstallationSubTaskContributor
{
    public void AddTasks(DownloadInstallationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var task = context.ParentTask.CreateSubTask(
            "Download vanilla libraries",
            50,
            async (cancellationToken, dependencies, progress) =>
            {
                var libraries = VanillaGameDownloader.GetLibrariesForDownload(context.GetRequiredVersionInfo());
                var executor = VanillaGameSubTaskFactory.CreateLibrariesExecutor(
                    context.VanillaDownloader,
                    libraries,
                    context.Request.RootPath);
                return await executor(cancellationToken, dependencies, progress);
            },
            dependencies: [context.Tasks.VersionInfoTask!],
            translationKey: "Pages.TaskPage.Tasks.GameInstall.Vanilla.DownloadLibraries");

        context.Tasks.SetLibrariesTask(task);
    }
}
