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
using System.Threading;
using System.Threading.Tasks;

public class TaskManager(int maxConcurrency) : IDisposable
{
    private static TaskManager? s_instance;
    public static TaskManager Instance => s_instance ??= new TaskManager(4);
    
    private readonly PriorityQueue<SubTaskBase, int> _queue = new();
    private readonly SemaphoreSlim _semaphore = new(maxConcurrency);
    private readonly List<ParentTask> _parents = new();
    private readonly CancellationTokenSource _managerCts = new(); 
    private readonly HashSet<ParentTask> _faultedParents = new();
    private Task? _schedulerTask; 

    public void Start()
    {
        if (_schedulerTask is { Status: TaskStatus.Running }) return;
        _schedulerTask = Task.Run(SchedulerLoopAsync, _managerCts.Token);
    }

    public void Stop()
    {
        _managerCts.Cancel();
        _schedulerTask?.Wait();
    }
    
    async private Task SchedulerLoopAsync()
    {
        while (!_managerCts.IsCancellationRequested)
        {
            // 移除已完成/失败/取消的父任务
            _parents.RemoveAll(p => p.IsFinished);

            foreach (var parent in _parents.ToArray())
            {
                // 只对等待中的父任务入队子任务
                if (parent.State != TaskState.Waiting)
                    continue;
                foreach (var sub in parent.SubTasks.Where(s => !s.IsFinished))
                    _queue.Enqueue(sub, sub.Priority);
            }
            
            if (_queue.Count > 0)
                await RunOnceAsync();

            await Task.Delay(100, _managerCts.Token);
        }
    }
    
    internal void RegisterFaultedParent(ParentTask parent)
    {
        _faultedParents.Add(parent);
    }
    
    
    public ParentTask CreateParent(string name)
    {
        var p = new ParentTask(name);
        _parents.Add(p);
        return p;
    }

    public IReadOnlyList<ParentTask> GetParents() => _parents.AsReadOnly();

    async private Task RunOnceAsync()
    {
        var running = new List<Task>();
        while (_queue.Count > 0 && !_managerCts.IsCancellationRequested)
        {
            var task = _queue.Dequeue();
            
            // 跳过已完成的或属于已失败父任务的任务
            if (task.IsFinished) continue;
            if (_faultedParents.Contains(task.Parent)) continue;

            await WaitDependencies(task);
            if (task.IsFinished) continue;
            if (_faultedParents.Contains(task.Parent)) continue;

            await _semaphore.WaitAsync(_managerCts.Token);
            running.Add(Task.Run(async () =>
            {
                try { await task.ExecuteAsync(); }
                catch { /* 忽略异常，任务状态已由 TaskBase 设置 */ }
                finally { _semaphore.Release(); }
            }, _managerCts.Token));

            if (_queue.Count == 0 || _queue.Peek().Priority != task.Priority)
            {
                try { await Task.WhenAll(running); }
                catch { /* 忽略异常，任务状态已由 TaskBase 设置 */ }
                running.Clear();
            }
        }
    }
    
    async private Task WaitDependencies(SubTaskBase task)
    {
        foreach (var dep in task.Dependencies)
            while (!dep.IsFinished)
                await Task.Delay(10);
    }

    public void Dispose()
    {
        foreach (var p in _parents) p.Cancel();
        _semaphore.Dispose();
    }
}