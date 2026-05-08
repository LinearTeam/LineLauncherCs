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
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using FluentAvalonia.UI.Controls;
using LMCCore.Tasks;
using LMCCore.Tasks.Model;

namespace LMCUI.Pages.TaskPage;

public partial class TaskPage : PageBase
{
    private readonly ObservableCollection<ParentTask> _taskList = [];
    private readonly Dictionary<Guid, (FASettingsExpander Expander, StackPanel HeaderPanel, StackPanel FooterPanel, TextBlock CountTextBlock)> _parentControls = [];
    private readonly Dictionary<Guid, (FASettingsExpanderItem Item, StackPanel ContentPanel, StackPanel FooterPanel, ProgressBar ProgressBar)> _subTaskControls = [];
    
    // UI更新批处理机制
    private readonly Queue<Action> _pendingUiUpdates = [];
    private readonly object _uiUpdateLock = new();
    private DispatcherTimer? _uiUpdateTimer;

    public TaskPage() : base("Pages.TaskPage.Title", "TaskPage")
    {
        InitializeComponent();
        TaskListView.Instance = this;
        
        // 初始化UI更新批处理定时器
        _uiUpdateTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(16) // ~60fps
        };
        _uiUpdateTimer.Tick += ProcessUiUpdates;

        Loaded += (_, _) =>
        {
            RefreshTaskList();
            _uiUpdateTimer?.Start();
            // 监听 TaskManager 的新任务添加事件
            TaskManager.Instance.ParentTaskAdded += OnParentTaskAdded;
        };
        
        Unloaded += (_, _) =>
        {
            _uiUpdateTimer?.Stop();
            TaskManager.Instance.ParentTaskAdded -= OnParentTaskAdded;
        };
    }

    private void OnParentTaskAdded(ParentTask parent)
    {
        // 在 UI 线程上添加新任务
        Dispatcher.UIThread.Post(() =>
        {
            if (_taskList.Any(t => t.Id == parent.Id))
                return;
            
            _taskList.Insert(0, parent);
            parent.PropertyChanged += Parent_PropertyChanged;

            foreach (var sub in parent.SubTasks)
            {
                sub.PropertyChanged += SubTask_PropertyChanged;
            }

            // 只为新任务创建 UI，不重建整个列表
            if (parent.State is not TaskState.Canceled and not TaskState.Completed)
            {
                var expander = CreateParentTaskExpander(parent);
                TaskList.Children.Insert(0, expander);
            }
        });
    }

    /// <summary>
    /// 批量处理待执行的UI更新，减少线程切换和UI刷新次数
    /// </summary>
    private void ProcessUiUpdates(object? sender, EventArgs e)
    {
        List<Action> updates;
        lock (_uiUpdateLock)
        {
            if (_pendingUiUpdates.Count == 0)
                return;
            
            updates = _pendingUiUpdates.ToList();
            _pendingUiUpdates.Clear();
        }
        
        foreach (var update in updates)
        {
            update();
        }
    }

    /// <summary>
    /// 将UI操作加入批处理队列
    /// </summary>
    private void QueueUiUpdate(Action update)
    {
        lock (_uiUpdateLock)
        {
            _pendingUiUpdates.Enqueue(update);
        }
    }

    private void RefreshTaskList()
    {
        var currentParents = TaskManager.Instance.GetParents();

        foreach (var parent in currentParents)
        {
            if (_taskList.Any(t => t.Id == parent.Id))
                continue;
            _taskList.Insert(0, parent);
            parent.PropertyChanged += Parent_PropertyChanged;

            foreach (var sub in parent.SubTasks)
            {
                sub.PropertyChanged += SubTask_PropertyChanged;
            }
        }

        for (var i = _taskList.Count - 1; i >= 0; i--)
        {
            var item = _taskList[i];
            if (!currentParents.Contains(item))
            {
                item.PropertyChanged -= Parent_PropertyChanged;
                foreach (var sub in item.SubTasks)
                {
                    sub.PropertyChanged -= SubTask_PropertyChanged;
                }
                _taskList.RemoveAt(i);
            }
        }

        RebuildTaskList();
    }

    private void Parent_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (sender is not ParentTask parent)
            return;

        QueueUiUpdate(() =>
        {
            if (!_parentControls.TryGetValue(parent.Id, out var controls))
                return;

            switch (e.PropertyName)
            {
                case nameof(TaskBase.State):
                    UpdateParentStateIcon(controls.HeaderPanel, parent.State);
                    UpdateParentCancelButton(controls.Expander, parent, controls.FooterPanel, controls.CountTextBlock);
                    if (parent.State is TaskState.Canceled or TaskState.Completed)
                    {
                        RemoveParentTask(parent);
                    }
                    break;

                case nameof(ParentTask.CompletedCount):
                case nameof(ParentTask.TotalCount):
                    controls.CountTextBlock.Text = $"{parent.CompletedCount} / {parent.TotalCount}";
                    break;
            }
        });
    }

    private void SubTask_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (sender is not SubTaskBase subTask)
            return;

        if (!_subTaskControls.TryGetValue(subTask.Id, out var controls))
            return;

        switch (e.PropertyName)
        {
            case nameof(TaskBase.State):
                QueueUiUpdate(() =>
                {
                    if (!_subTaskControls.TryGetValue(subTask.Id, out var c))
                        return;
                    UpdateSubTaskStateIcon(c.Item, c.ContentPanel, subTask);
                    UpdateSubTaskProgress(c.Item, c.ProgressBar, subTask);
                    UpdateParentStateBasedOnSubTasks(subTask.Parent);
                });
                break;
                
            case nameof(SubTask<object>.Progress):
                // 进度变化时立即更新，不节流（任务完成时需要立即显示100%）
                QueueUiUpdate(() =>
                {
                    if (!_subTaskControls.TryGetValue(subTask.Id, out var c))
                        return;
                    UpdateSubTaskProgress(c.Item, c.ProgressBar, subTask);
                });
                break;
        }
    }

    private void UpdateSubTaskProgress(FASettingsExpanderItem item, ProgressBar progressBar, SubTaskBase subTask)
    {
        var hasProgress = subTask.Progress >= 0;
        progressBar.IsIndeterminate = !hasProgress;
        progressBar.ShowProgressText = hasProgress;
        progressBar.Value = hasProgress ? subTask.Progress : 0;
        progressBar.IsVisible = subTask.State == TaskState.Running;
    }

    private void UpdateParentStateBasedOnSubTasks(ParentTask parent)
    {
        if (!_parentControls.TryGetValue(parent.Id, out var controls))
            return;

        var state = GetParentDisplayState(parent);
        UpdateParentStateIcon(controls.HeaderPanel, state);
        controls.CountTextBlock.Text = $"{parent.CompletedCount} / {parent.TotalCount}";
        UpdateParentCancelButton(controls.Expander, parent, controls.FooterPanel, controls.CountTextBlock);
    }

    private TaskState GetParentDisplayState(ParentTask parent)
    {
        if (parent.SubTasks.Any(s => s.State == TaskState.Running))
            return TaskState.Running;
        if (parent.SubTasks.Any(s => s.State == TaskState.Faulted))
            return TaskState.Faulted;
        if (parent.SubTasks.All(s => s.State == TaskState.Completed))
            return TaskState.Completed;
        if (parent.SubTasks.Any(s => s.State == TaskState.Canceled))
            return TaskState.Canceled;
        return TaskState.Waiting;
    }

    private void RebuildTaskList()
    {
        TaskList.Children.Clear();
        _parentControls.Clear();
        _subTaskControls.Clear();

        foreach (var parent in _taskList)
        {
            if(parent.State is TaskState.Canceled or TaskState.Completed) continue;
            var expander = CreateParentTaskExpander(parent);
            TaskList.Children.Add(expander);
        }
    }

    private FASettingsExpander CreateParentTaskExpander(ParentTask parent)
    {
        var displayState = GetParentDisplayState(parent);
        var headerPanel = TaskItemFactory.CreateHeaderPanel(parent, displayState);

        var expander = new FASettingsExpander
        {
            Header = headerPanel,
            IsExpanded = true
        };

        var footer = TaskItemFactory.CreateFooterPanel(parent);
        expander.Footer = footer;

        var countText = (TextBlock)footer.Children.First();
        _parentControls[parent.Id] = (expander, headerPanel, footer, countText);

        foreach (var item in parent.SubTasks.Select(CreateSubTaskItem))
        {
            expander.Items.Add(item);
        }

        UpdateParentCancelButton(expander, parent, footer, countText);

        return expander;
    }

    private void UpdateParentStateIcon(StackPanel headerPanel, TaskState state)
    {
        if (headerPanel.Children.Count > 0 && headerPanel.Children[0] is Control oldIcon)
        {
            var index = headerPanel.Children.IndexOf(oldIcon);
            headerPanel.Children[index] = TaskItemFactory.CreateStateControl(state);
        }
    }

    private void UpdateParentCancelButton(FASettingsExpander expander, ParentTask parent, StackPanel footerPanel, TextBlock countTextBlock)
    {
        var state = GetParentDisplayState(parent);
        UpdateParentCancelButton(expander, parent, state, footerPanel);
    }

    private void UpdateParentCancelButton(FASettingsExpander expander, ParentTask parent, TaskState state, StackPanel footerPanel)
    {
        // 移除现有的按钮
        var existingButton = footerPanel.Children.OfType<Button>().FirstOrDefault();
        if (existingButton != null)
        {
            footerPanel.Children.Remove(existingButton);
        }

        if (state is TaskState.Waiting or TaskState.Running)
        {
            var cancelButton = TaskItemFactory.CreateCancelButton(parent);
            cancelButton.Click += CancelParentTask;
            footerPanel.Children.Insert(footerPanel.Children.Count, cancelButton);
        }
        else if (state is TaskState.Faulted or TaskState.Completed)
        {
            var confirmButton = TaskItemFactory.CreateConfirmButton(parent);
            confirmButton.Click += DismissParentTask;
            footerPanel.Children.Insert(footerPanel.Children.Count, confirmButton);
        }
    }

    private void CancelParentTask(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: ParentTask parent })
        {
            parent.Cancel();
        }
    }

    private void DismissParentTask(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: ParentTask parent })
        {
            RemoveParentTask(parent);
        }
    }

    private void RemoveParentTask(ParentTask parent)
    {
        if (!_parentControls.TryGetValue(parent.Id, out var controls))
            return;

        parent.PropertyChanged -= Parent_PropertyChanged;
        foreach (var sub in parent.SubTasks)
        {
            sub.PropertyChanged -= SubTask_PropertyChanged;
        }

        TaskManager.Instance.RemoveParent(parent);
        _taskList.Remove(parent);
        _parentControls.Remove(parent.Id);
        TaskList.Children.Remove(controls.Expander);
    }

    private FASettingsExpanderItem CreateSubTaskItem(SubTaskBase subTask)
    {
        var contentPanel = TaskItemFactory.CreateSubTaskContentPanel(subTask);

        var item = new FASettingsExpanderItem
        {
            Content = contentPanel,
            IsEnabled = subTask.State is TaskState.Waiting or TaskState.Running
        };

        var footerPanel = TaskItemFactory.CreateSubTaskFooterPanel(subTask);
        var progressBar = (ProgressBar)footerPanel.Children.First();
        item.Footer = footerPanel;

        _subTaskControls[subTask.Id] = (item, contentPanel, footerPanel, progressBar);

        return item;
    }

    private void UpdateSubTaskStateIcon(FASettingsExpanderItem? item, StackPanel contentPanel, SubTaskBase subTask)
    {
        if (contentPanel.Children.Count > 0 && contentPanel.Children[0] is Control oldIcon)
        {
            var index = contentPanel.Children.IndexOf(oldIcon);
            contentPanel.Children[index] = TaskItemFactory.CreateStateControl(subTask.State);
        }
        else
        {
            contentPanel.Children.Insert(0, TaskItemFactory.CreateStateControl(subTask.State));
        }

        if (item != null)
        {
            item.IsEnabled = subTask.State is TaskState.Waiting or TaskState.Running;
        }
    }

    public static class TaskListView
    {
        public static TaskPage? Instance { get; set; }
    }
}
