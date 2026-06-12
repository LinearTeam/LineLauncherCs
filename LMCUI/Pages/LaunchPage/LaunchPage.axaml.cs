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
using System.Threading.Tasks;
using Avalonia.Interactivity;
using FluentAvalonia.UI.Controls;
using LMC;
using LMC.Basic.Logging;
using LMCCore.Game.Download;
using LMCCore.Game.Download.Model;
using LMCUI.I18n;
using LMCUI.Navigation;
using LMCUI.Navigation.Model;
using LMCUI.Utils;

namespace LMCUI.Pages.LaunchPage;

public partial class LaunchPage : PageBase
{
    public LaunchPage() : base("Pages.LaunchPage.Title", "LaunchPage")
    {
        InitializeComponent();
    }

    async private void InstallVanillaButton_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            var gameRoot = Current.Config.SelectedGameRootPath;
            if (string.IsNullOrWhiteSpace(gameRoot))
            {
                await MessageQueueHelper.ShowError(
                    I18nManager.Instance.GetString("Pages.DownloadMinecraftPage.Wizard.Errors.NoRootTitle"),
                    I18nManager.Instance.GetString("Pages.DownloadMinecraftPage.Wizard.Errors.NoRootContent"));
                return;
            }

            var downloadManager = new DownloadManager();
            var request = new DownloadableGameVersion
            {
                RootPath = gameRoot,
                VersionId = "1.21",
                VersionName = "1.21",
                Loaders = []
            };

            await downloadManager.CreateDownloadPlanAsync(request);
            NavigateToTaskPageButton_Click(sender, e);
        }
        catch (Exception ex)
        {
            new Logger("LP").Error(ex, "Creating Task");
            await MessageQueueHelper.ShowError(
                I18nManager.Instance.GetString("Pages.DownloadMinecraftPage.Errors.LoadFailedTitle"),
                ex.Message);
        }
    }

    private void NavigateToTaskPageButton_Click(object? sender, RoutedEventArgs e)
    {
        MainWindow.NavigatePage(new PageNavigateWay(
            typeof(TaskPage.TaskPage),
            (FANavigationViewItem)MainWindow.Instance.mnv.SelectedItem
        ), NavigateType.New);
    }
}
