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
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using FluentAvalonia.UI.Controls;
using LMCCore.Tasks.Model;
using LMCUI.I18n;

namespace LMCUI.Pages.TaskPage;

public static class TaskItemFactory
{
    public static Control CreateStateControl(TaskState state)
    {
        return state switch
        {
            TaskState.Waiting => new TextBlock { Text = "", VerticalAlignment = VerticalAlignment.Center },
            TaskState.Running => new FAProgressRing
            {
                IsIndeterminate = true,
                Width = 16,
                Height = 16,
                VerticalAlignment = VerticalAlignment.Center
            },
            TaskState.Completed => new FASymbolIcon
            {
                Symbol = FASymbol.Accept,
                Foreground = new SolidColorBrush(Color.Parse("#4CAF50"))
            },
            TaskState.Faulted => new FASymbolIcon
            {
                Symbol = FASymbol.Clear,
                Foreground = new SolidColorBrush(Color.Parse("#F44336"))
            },
            TaskState.Canceled => new FASymbolIcon
            {
                Symbol = FASymbol.Clear,
                Foreground = new SolidColorBrush(Color.Parse("#9E9E9E"))
            },
            _ => throw new ArgumentOutOfRangeException(nameof(state))
        };
    }

    public static StackPanel CreateHeaderPanel(ParentTask parent, TaskState displayState)
    {
        var headerPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8
        };

        headerPanel.Children.Add(CreateStateControl(displayState));
        headerPanel.Children.Add(new TextBlock { Text = parent.Name, VerticalAlignment = VerticalAlignment.Center });

        return headerPanel;
    }

    public static StackPanel CreateFooterPanel(ParentTask parent)
    {
        var footer = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8
        };
        var countText = new TextBlock
        {
            Text = $"{parent.CompletedCount} / {parent.TotalCount}",
            VerticalAlignment = VerticalAlignment.Center
        };
        footer.Children.Add(countText);
        return footer;
    }

    public static Button CreateCancelButton(ParentTask parent)
    {
        var cancelButton = new Button
        {
            Content = new TextBlock { Text = I18nManager.Instance.GetString("Pages.TaskPage.CancelButton") },
            Tag = parent,
            Margin = new Avalonia.Thickness(0, 0, 20, 0),
        };
        return cancelButton;
    }

    public static Button CreateConfirmButton(ParentTask parent)
    {
        var confirmButton = new Button
        {
            Content = new TextBlock { Text = I18nManager.Instance.GetString("Pages.TaskPage.ConfirmButton") },
            Tag = parent,
            Margin = new Avalonia.Thickness(0, 0, 20, 0),
        };
        confirmButton.Classes.Clear();
        confirmButton.Classes.Add("accent");
        return confirmButton;
    }

    public static StackPanel CreateSubTaskContentPanel(SubTaskBase subTask)
    {
        var contentPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 3
        };

        contentPanel.Children.Add(CreateStateControl(subTask.State));
        contentPanel.Children.Add(new TextBlock { Text = subTask.Name });

        return contentPanel;
    }

    public static StackPanel CreateSubTaskFooterPanel(SubTaskBase subTask)
    {
        var footerPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            Margin = new Avalonia.Thickness(20, 5, 0, 0),
            VerticalAlignment = VerticalAlignment.Center
        };

        var hasProgress = subTask.Progress >= 0;
        var progressBar = new ProgressBar()
        {
            ShowProgressText = hasProgress,
            IsIndeterminate = !hasProgress,
            Value = subTask.Progress,
            Width = 70,
            IsVisible = subTask.State == TaskState.Running
        };
        footerPanel.Children.Add(progressBar);

        return footerPanel;
    }

    public static ProgressBar CreateSubTaskProgressBar(SubTaskBase subTask)
    {
        var hasProgress = subTask.Progress >= 0;
        return new ProgressBar()
        {
            ShowProgressText = hasProgress,
            IsIndeterminate = !hasProgress,
            Value = subTask.Progress,
            Width = 70,
            IsVisible = subTask.State == TaskState.Running
        };
    }
}
