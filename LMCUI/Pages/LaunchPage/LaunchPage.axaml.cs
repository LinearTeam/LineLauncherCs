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
using LMCCore.Tasks;
using LMCCore.Tasks.Model;
using LMCUI.Utils;

namespace LMCUI.Pages.LaunchPage;

public partial class LaunchPage : PageBase
{
    public LaunchPage() : base("Pages.LaunchPage.Title","LaunchPage")
    {
        InitializeComponent();
    }

    private void ShowInfoButton_Click(object? sender, RoutedEventArgs e)
    {
        MessageQueueHelper.ShowInfo("信息提示", "这是一个信息提示示例，将在5秒后自动消失或手动关闭。");
    }

    private void ShowSuccessButton_Click(object? sender, RoutedEventArgs e)
    {
        MessageQueueHelper.ShowSuccess("成功提示", "操作成功完成！这是一个成功提示示例。");
    }

    private void ShowWarningButton_Click(object? sender, RoutedEventArgs e)
    {
        MessageQueueHelper.ShowWarning("警告提示", "这是一个警告提示，请注意潜在问题。");
    }

    private void ShowErrorButton_Click(object? sender, RoutedEventArgs e)
    {
        MessageQueueHelper.ShowError("错误提示", "发生了一个错误，请检查操作是否正确。");
    }

    private void ShowTeachingTipButton_Click(object? sender, RoutedEventArgs e)
    {
        MessageQueueHelper.ShowTeachingTip("使用提示", "这是一个教学提示，用于引导用户完成特定操作。");
    }

    private void CreateSampleTaskButton_Click(object? sender, RoutedEventArgs e)
    {
        var parent = TaskManager.Instance.CreateParent("示例任务");

        parent.CreateSubTask<int>("步骤1: 准备", 0, async (ct, deps, progress) =>
        {
            for (int i = 0; i <= 100; i += 10)
            {
                progress.Report(i);
                await Task.Delay(200, ct);
            }
            return 100;
        });

        var step2 = parent.CreateSubTask<int>("步骤2: 处理", 1, async (ct, deps, progress) =>
        {
            for (int i = 0; i <= 100; i += 5)
            {
                progress.Report(i);
                await Task.Delay(100, ct);
            }
            return 200;
        });

        parent.CreateSubTask<int>("步骤3: 等待 (会失败)", 2, async (ct, deps, progress) =>
        {
            progress.Report(0);
            await Task.Delay(500, ct);
            progress.Report(50);
            throw new InvalidOperationException("这是一个模拟的错误！");
        }, new[] { step2 });

        parent.CreateSubTask<int>("步骤4: 清理", 3, async (ct, deps, progress) =>
        {
            for (int i = 0; i <= 100; i += 20)
            {
                progress.Report(i);
                await Task.Delay(100, ct);
            }
            return 400;
        });

        MessageQueueHelper.ShowInfo("任务已创建", "示例任务已创建，请前往任务页面查看。");
    }

    private void NavigateToTaskPageButton_Click(object? sender, RoutedEventArgs e)
    {
        MainWindow.NavigatePage(new PageNavigateWay(
            typeof(TaskPage.TaskPage),
            (NavigationViewItem)MainWindow.Instance.mnv.SelectedItem
        ), NavigateType.New);
    }
}