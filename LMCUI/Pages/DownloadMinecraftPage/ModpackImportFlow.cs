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

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using FluentAvalonia.UI.Controls;
using LMC.Basic.Logging;
using LMCCore.Game.Download;
using LMCCore.Game.Download.Model;
using LMCCore.Game.Versioning;
using LMCCore.Tasks.Model;
using LMCUI.I18n;
using LMCUI.Navigation.Model;
using LMCUI.Pages.TaskPage;

namespace LMCUI.Pages.DownloadMinecraftPage;

internal static class ModpackImportFlow
{
    private static readonly Logger s_logger = new("ModpackImportFlow");
    private static readonly SemaphoreSlim s_importGate = new(1, 1);

    public static async Task InstallAsync(string archivePath)
    {
        s_logger.Info($"Modpack import requested: '{archivePath}'.");
        if (!await s_importGate.WaitAsync(0))
        {
            s_logger.Info("Modpack import ignored: another import dialog is already open.");
            await LMCUI.Utils.MessageQueueHelper.ShowInfo(
                I18nManager.Instance.GetString("Pages.ModpackImport.Title"),
                I18nManager.Instance.GetString("Pages.ModpackImport.AlreadyOpen"));
            return;
        }
        try
        {
            DownloadableGameVersion? request;
            try
            {
                s_logger.Info("Opening modpack import dialog.");
                request = await new ModrinthModpackImportDialog().ShowAsync(archivePath);
            }
            catch (Exception ex)
            {
                s_logger.Error(ex, "Inspecting modpack archive");
                await LMCUI.Utils.MessageQueueHelper.ShowError(
                    I18nManager.Instance.GetString("Pages.ModpackImport.ErrorTitle"), LocalizeReaderError(ex));
                return;
            }
            if (request == null)
            {
                s_logger.Info("Modpack import dialog closed without an installation request.");
                return;
            }

            var taskItem = MainWindow.Instance.mnv.FooterMenuItems
                .OfType<FANavigationViewItem>()
                .First(item => string.Equals(item.Tag?.ToString(), "TaskPage", StringComparison.Ordinal));
            MainWindow.NavigatePage(
                new PageNavigateWay(typeof(LMCUI.Pages.TaskPage.TaskPage), taskItem), NavigateType.New);

            try
            {
                var plan = await new DownloadManager().CreateDownloadPlanAsync(request);
                s_logger.Info("Modpack installation plan created; installation handed to task manager.");
                ObserveCompletion(plan);
            }
            catch (Exception ex)
            {
                s_logger.Error(ex, "Creating modpack installation plan");
                await LMCUI.Utils.MessageQueueHelper.ShowError(
                    I18nManager.Instance.GetString("Pages.ModpackImport.ErrorTitle"),
                    I18nManager.Instance.GetString("Pages.ModpackImport.InstallFailed"));
            }
        }
        finally
        {
            s_importGate.Release();
        }
    }

    public static string LocalizeReaderError(Exception exception)
    {
        var message = exception.Message;
        var key = message.Contains("Unsupported modpack dependency", StringComparison.Ordinal)
            ? "UnsupportedDependency"
            : message.Contains("not a Modrinth modpack", StringComparison.Ordinal)
                ? "InvalidArchive"
                : "ReadFailed";
        return I18nManager.Instance.GetString("Pages.ModpackImport." + key, exception.Message);
    }

    private static void ObserveCompletion(DownloadGamePlan plan)
    {
        var finalizer = plan.FinalizationTask;
        if (finalizer == null)
            return;
        var handled = 0;
        void OnCompleted(SubTaskBase task)
        {
            if (Interlocked.Exchange(ref handled, 1) != 0)
                return;
            finalizer.Completed -= OnCompleted;
            if (task.State == TaskState.Completed)
                _ = RefreshCatalogAsync(plan.Request.RootPath);
        }
        finalizer.Completed += OnCompleted;
        if (finalizer.IsFinished)
            OnCompleted(finalizer);
    }

    private static async Task RefreshCatalogAsync(string rootPath)
    {
        try
        {
            var versions = await new VersionManager().RefreshVersionsAsync(rootPath);
            await Dispatcher.UIThread.InvokeAsync(() => VersionCatalogRefreshCoordinator.Publish(rootPath, versions));
        }
        catch (Exception ex)
        {
            s_logger.Error(ex, $"Refreshing version catalog after modpack install in {rootPath}");
        }
    }
}
