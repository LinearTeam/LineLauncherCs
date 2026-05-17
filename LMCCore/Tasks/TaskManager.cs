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

using LMCCore.Tasks.Model;

namespace LMCCore.Tasks;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

public class TaskManager(int maxConcurrency) : IDisposable
{
    private static TaskManager? s_instance;
    public static TaskManager Instance => s_instance ??= new TaskManager(4);

    private readonly PriorityQueue<SubTaskBase, int> _queue = new();
    private readonly SemaphoreSlim _semaphore = new(maxConcurrency);
    private readonly SemaphoreSlim _signal = new(0, int.MaxValue);
    private readonly object _syncRoot = new();
    private readonly List<ParentTask> _parents = [];
    private readonly HashSet<ParentTask> _faultedParents = [];
    private readonly HashSet<SubTaskBase> _queuedTasks = [];
    private readonly HashSet<SubTaskBase> _activeTasks = [];
    private readonly CancellationTokenSource _managerCts = new();
    private Task? _schedulerTask;

    public event Action<ParentTask>? ParentTaskAdded;

    public void Start()
    {
        if (_schedulerTask is { IsCompleted: false })
        {
            return;
        }

        _schedulerTask = Task.Run(SchedulerLoopAsync, _managerCts.Token);
    }

    public async Task StopAsync()
    {
        _managerCts.Cancel();
        _signal.Release();

        if (_schedulerTask == null)
        {
            return;
        }

        try
        {
            await _schedulerTask.ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
        }
    }

    public void Stop()
    {
        StopAsync().GetAwaiter().GetResult();
    }

    internal void Signal()
    {
        _signal.Release();
    }

    internal void RegisterFaultedParent(ParentTask parent)
    {
        lock (_syncRoot)
        {
            _faultedParents.Add(parent);
        }

        Signal();
    }

    internal void OnSubTaskAdded(SubTaskBase subTask)
    {
        lock (_syncRoot)
        {
            RegisterDependencyHandlersUnsafe(subTask);
            TryEnqueueUnsafe(subTask);
        }

        Signal();
    }

    public ParentTask CreateParent(string name)
    {
        var parent = new ParentTask(name);
        lock (_syncRoot)
        {
            _parents.Add(parent);
        }

        Signal();
        ParentTaskAdded?.Invoke(parent);
        return parent;
    }

    public IReadOnlyList<ParentTask> GetParents()
    {
        lock (_syncRoot)
        {
            return _parents.ToArray();
        }
    }

    public void RemoveParent(ParentTask parent)
    {
        lock (_syncRoot)
        {
            _parents.Remove(parent);
        }
    }

    private async Task SchedulerLoopAsync()
    {
        while (!_managerCts.IsCancellationRequested)
        {
            try
            {
                await _signal.WaitAsync(_managerCts.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            while (true)
            {
                SubTaskBase? nextTask;
                lock (_syncRoot)
                {
                    _parents.RemoveAll(parent => parent.IsFinished);
                    EnqueueReadyTasksUnsafe();

                    if (_queue.Count == 0)
                    {
                        break;
                    }

                    nextTask = _queue.Dequeue();
                    _queuedTasks.Remove(nextTask);
                    _activeTasks.Add(nextTask);
                }

                if (!CanExecute(nextTask))
                {
                    lock (_syncRoot)
                    {
                        _activeTasks.Remove(nextTask);
                    }
                    continue;
                }

                try
                {
                    await _semaphore.WaitAsync(_managerCts.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    lock (_syncRoot)
                    {
                        _activeTasks.Remove(nextTask);
                    }
                    break;
                }

                _ = ExecuteTaskAsync(nextTask);
            }
        }
    }

    private async Task ExecuteTaskAsync(SubTaskBase task)
    {
        try
        {
            await task.ExecuteAsync().ConfigureAwait(false);
        }
        catch
        {
            // TaskBase/ParentTask already owns state transition and fault propagation.
        }
        finally
        {
            lock (_syncRoot)
            {
                _activeTasks.Remove(task);
            }
            _semaphore.Release();
            Signal();
        }
    }

    private bool CanExecute(SubTaskBase task)
    {
        if (task.IsFinished)
        {
            return false;
        }

        lock (_syncRoot)
        {
            if (_faultedParents.Contains(task.Parent) || task.Parent.State == TaskState.Canceled)
            {
                return false;
            }
        }

        return AreDependenciesSatisfied(task);
    }

    private void DependencyCompleted(SubTaskBase _)
    {
        lock (_syncRoot)
        {
            EnqueueReadyTasksUnsafe();
        }

        Signal();
    }

    private void EnqueueReadyTasksUnsafe()
    {
        foreach (var parent in _parents)
        {
            if (parent.State != TaskState.Waiting)
            {
                continue;
            }

            foreach (var subTask in parent.SubTasks)
            {
                RegisterDependencyHandlersUnsafe(subTask);
                TryEnqueueUnsafe(subTask);
            }
        }
    }

    private void RegisterDependencyHandlersUnsafe(SubTaskBase subTask)
    {
        foreach (var dependency in subTask.Dependencies)
        {
            dependency.Completed -= DependencyCompleted;
            dependency.Completed += DependencyCompleted;
        }
    }

    private void TryEnqueueUnsafe(SubTaskBase subTask)
    {
        if (subTask.IsFinished || subTask.IsExecuting || _queuedTasks.Contains(subTask) || _activeTasks.Contains(subTask))
        {
            return;
        }

        if (_faultedParents.Contains(subTask.Parent) || subTask.Parent.State == TaskState.Canceled)
        {
            return;
        }

        if (!AreDependenciesSatisfied(subTask))
        {
            return;
        }

        _queue.Enqueue(subTask, subTask.Priority);
        _queuedTasks.Add(subTask);
    }

    private static bool AreDependenciesSatisfied(SubTaskBase subTask) =>
        subTask.Dependencies.All(dependency => dependency.State == TaskState.Completed);

    public void Dispose()
    {
        foreach (var parent in GetParents())
        {
            parent.Cancel();
        }

        _managerCts.Cancel();
        _semaphore.Dispose();
        _signal.Dispose();
        _managerCts.Dispose();
    }
}
