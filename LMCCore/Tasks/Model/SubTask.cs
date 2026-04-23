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
using System.Collections.Generic;
using System.Threading.Tasks;

public abstract class SubTaskBase(string name, int priority, ParentTask parent, IEnumerable<SubTaskBase>? deps)
    : TaskBase(name)
{
    public int Priority { get; } = priority;
    public IReadOnlyList<SubTaskBase> Dependencies { get; } = deps?.ToList() ?? new List<SubTaskBase>();
    public ParentTask Parent { get; } = parent;

}

public class SubTask<T>(
    string name,
    int priority,
    ParentTask parent,
    IEnumerable<SubTaskBase>? deps,
    Func<CancellationToken, Dictionary<SubTaskBase, object>, IProgress<int>, Task<T>> execute)
    : SubTaskBase(name, priority, parent, deps)
{

    public T? Result { get; private set; }

    public async override Task ExecuteAsync()
    {
        try
        {
            State = TaskState.Running;

            var deps = new Dictionary<SubTaskBase, object>();
            foreach (var d in Dependencies)
            {
                if (d is SubTask<object> s)
                    deps[d] = s.Result!;
            }

            var progress = new Progress<int>(p =>
                Console.WriteLine($"[{Name}] {p}%"));

            Result = await execute(Cts.Token, deps, progress);
            State = TaskState.Completed;
        }
        catch (OperationCanceledException)
        {
            State = TaskState.Canceled;
        }
        catch (Exception ex)
        {
            State = TaskState.Faulted;
            Parent.OnSubTaskFaulted(this);
            throw;
        }
    }
}