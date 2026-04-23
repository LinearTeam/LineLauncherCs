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
    private readonly PriorityQueue<SubTaskBase, int> _queue = new();
    private readonly SemaphoreSlim _semaphore = new(maxConcurrency);
    private readonly List<ParentTask> _parents = new();

    public ParentTask CreateParent(string name)
    {
        var p = new ParentTask(name);
        _parents.Add(p);
        return p;
    }

    public void Enqueue(SubTaskBase task)
        => _queue.Enqueue(task, task.Priority);

    public async Task RunAsync()
    {
        var running = new List<Task>();

        while (_queue.Count > 0)
        {
            var task = _queue.Dequeue();

            if (task.IsFinished) continue;

            await WaitDependencies(task);

            if (task.IsFinished) continue;

            await _semaphore.WaitAsync();

            running.Add(Task.Run(async () =>
            {
                try
                {
                    await task.ExecuteAsync();
                }
                catch
                {
                    // 吞掉，异常已在任务内处理
                }
                finally
                {
                    _semaphore.Release();
                }
            }));

            if (_queue.Count == 0 || _queue.Peek().Priority != task.Priority)
            {
                await Task.WhenAll(running);
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