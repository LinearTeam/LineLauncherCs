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
using System.Linq;
using LMCCore.Tasks.Model;
using LMCUI.I18n;

namespace LMCUI.Pages.TaskPage;

internal enum ParentTaskActionButtonMode
{
    None,
    Cancel,
    Confirm
}

internal readonly record struct TaskPageDiff(
    IReadOnlyList<ParentTask> AddedParents,
    IReadOnlyList<ParentTask> RemovedParents);

internal static class TaskPagePresentation
{
    public static string GetTaskDisplayName(TaskBase task)
    {
        ArgumentNullException.ThrowIfNull(task);

        if (string.IsNullOrWhiteSpace(task.TranslationKey))
        {
            return task.Name;
        }

        return I18nManager.Instance.GetString(task.TranslationKey, [.. task.TranslationArgs]);
    }

    public static TaskState GetParentDisplayState(ParentTask parent)
    {
        return GetParentDisplayState(parent.SubTasks);
    }

    public static TaskState GetParentDisplayState(IEnumerable<SubTaskBase> subTasks)
    {
        var subTaskList = subTasks.ToList();
        if (subTaskList.Any(s => s.State == TaskState.Faulted))
        {
            return TaskState.Faulted;
        }

        if (subTaskList.Any(s => s.State == TaskState.Running))
        {
            return TaskState.Running;
        }

        if (subTaskList.Count > 0 && subTaskList.All(s => s.State == TaskState.Completed))
        {
            return TaskState.Completed;
        }

        if (subTaskList.Any(s => s.State == TaskState.Canceled))
        {
            return TaskState.Canceled;
        }

        return TaskState.Waiting;
    }

    public static bool ShouldRemoveParentTask(ParentTask parent)
    {
        return parent.State switch
        {
            TaskState.Canceled => parent.SubTasks.All(sub => !sub.IsExecuting),
            TaskState.Completed => true,
            _ => false
        };
    }

    public static ParentTaskActionButtonMode GetParentActionButtonMode(TaskState state)
    {
        return state switch
        {
            TaskState.Waiting or TaskState.Running => ParentTaskActionButtonMode.Cancel,
            TaskState.Faulted or TaskState.Completed => ParentTaskActionButtonMode.Confirm,
            _ => ParentTaskActionButtonMode.None
        };
    }

    public static IReadOnlyList<ParentTask> BuildVisibleParents(IEnumerable<ParentTask> parents)
    {
        return parents
            .Where(parent => parent.State is not (TaskState.Canceled or TaskState.Completed))
            .ToList();
    }

    public static TaskPageDiff DiffParents(
        IReadOnlyCollection<ParentTask> existingParents,
        IReadOnlyCollection<ParentTask> currentParents)
    {
        var existingById = existingParents.ToDictionary(parent => parent.Id);
        var currentById = currentParents.ToDictionary(parent => parent.Id);

        var added = currentParents
            .Where(parent => !existingById.ContainsKey(parent.Id))
            .ToList();
        var removed = existingParents
            .Where(parent => !currentById.ContainsKey(parent.Id))
            .ToList();

        return new TaskPageDiff(added, removed);
    }
}
