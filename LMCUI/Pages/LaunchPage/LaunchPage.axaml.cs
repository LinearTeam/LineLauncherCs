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
using System.IO;
using System.Threading.Tasks;
using Avalonia.Interactivity;
using FluentAvalonia.UI.Controls;
using LMC.Basic.Logging;
using LMCCore.Game.Download;
using LMCCore.Tasks;
using LMCCore.Tasks.Model;
using LMCUI.Navigation;
using LMCUI.Utils;

namespace LMCUI.Pages.LaunchPage;

public partial class LaunchPage : PageBase
{
    public LaunchPage() : base("Pages.LaunchPage.Title","LaunchPage")
    {
        InitializeComponent();
    }

    async private void InstallVanillaButton_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            var downloadManager = new DownloadManager();
            var versionInfo = await downloadManager.GetVersionInfoAsync("1.21");

            var gameRoot = Path.Combine(
                Environment.CurrentDirectory, ".minecraft");
            var libraryRoot = gameRoot;
            var assetRoot = Path.Combine(gameRoot, "assets");

            var parent = TaskManager.Instance.CreateParent($"安装 Minecraft {versionInfo.Id}");

            parent.CreateSubTask($"获取版本信息 ({versionInfo.Id})", 0, async (_, _, progress) =>
            {
                progress.Report(100);
                await Task.CompletedTask;
                return 100;
            });

            downloadManager.CreateVanillaGameSubTasks(parent, versionInfo, libraryRoot, assetRoot);

            MessageQueueHelper.ShowInfo("任务已创建", $"安装 Minecraft {versionInfo.Id} 的任务已创建，请前往任务页面查看。");
        }
        catch (Exception ex)
        {
            new Logger("LP").Error(ex, "Creating Task");
            MessageQueueHelper.ShowError("创建任务失败", $"无法创建安装任务：{ex.Message}");
        }
    }

    private void NavigateToTaskPageButton_Click(object? sender, RoutedEventArgs e)
    {
        MainWindow.NavigatePage(new PageNavigateWay(
            typeof(TaskPage.TaskPage),
            (NavigationViewItem)MainWindow.Instance.mnv.SelectedItem
        ), NavigateType.New);
    }
}