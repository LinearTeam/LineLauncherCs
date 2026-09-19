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
using LMCCore.Game.Download.Installation.Finalization;

namespace LMCCore.Game.Download.Installation.Providers;

public sealed class GameInstallationFinalizationTaskProvider : IGameInstallationTaskProvider
{
    private readonly GameInstallationFinalizer _finalizer;

    public GameInstallationFinalizationTaskProvider()
        : this(new GameInstallationFinalizer())
    {
    }

    internal GameInstallationFinalizationTaskProvider(GameInstallationFinalizer finalizer)
    {
        _finalizer = finalizer ?? throw new ArgumentNullException(nameof(finalizer));
    }

    public DownloadInstallationComponent Component => DownloadInstallationComponent.Finalization;

    public bool ShouldApply(DownloadInstallationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return true;
    }

    public void AddTasks(DownloadInstallationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var task = context.ParentTask.CreateSubTask(
            $"完成安装 ({context.Request.VersionName})",
            int.MaxValue,
            async (cancellationToken, _, progress) =>
            {
                await _finalizer.FinalizeAsync(context, cancellationToken);
                progress.Report(100);
                return true;
            },
            waitForSiblingTasksToComplete: true,
            translationKey: "Pages.TaskPage.Tasks.GameInstall.Finalize",
            translationArgs: [context.Request.VersionName]);

        context.Tasks.SetFinalizationTask(task);
    }
}
