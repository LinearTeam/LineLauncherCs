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

namespace LMCCore.Tasks.Model;

using System;
using System.Threading;
using System.Threading.Tasks;

public abstract class TaskBase(string name) : IDisposable
{
    public Guid Id { get; } = Guid.NewGuid();
    public string Name { get; } = name;

    public TaskState State { get; protected set; } = TaskState.Waiting;
    public bool IsFinished => State is TaskState.Completed or TaskState.Faulted or TaskState.Canceled;

    protected readonly CancellationTokenSource Cts = new();

    public void Cancel()
    {
        if (IsFinished) return;
        State = TaskState.Canceled;
        OnCancel();
        Cts.Cancel();
    }

    protected virtual void OnCancel() { }

    public abstract Task ExecuteAsync();

    public void Dispose() => Cts.Dispose();
}

public class TaskResult<T>
{
    public T? Value { get; init; }
    public Exception? Error { get; init; }
    public bool Success => Error == null;
}

public enum TaskState
{
    Waiting,
    Running,
    Completed,
    Faulted,
    Canceled
}