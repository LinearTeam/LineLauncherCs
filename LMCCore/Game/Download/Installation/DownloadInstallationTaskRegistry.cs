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
using LMCCore.Game.Model.LocalVersion;
using LMCCore.Tasks.Model;

namespace LMCCore.Game.Download.Installation;

public sealed class DownloadInstallationTaskRegistry
{
    private readonly Dictionary<string, SubTaskBase> _customTasks = [];

    public SubTask<LocalVersionInfo>? VersionInfoTask { get; private set; }

    public SubTask<string?>? ClientTask { get; private set; }

    public SubTask<List<DownloadableFileInfo>>? LibrariesTask { get; private set; }

    public SubTask<Dictionary<string, AssetInfo>>? AssetsTask { get; private set; }

    public SubTask<bool>? FinalizationTask { get; private set; }

    public SubTask<string>? FabricVersionJsonTask { get; private set; }

    public void SetVersionInfoTask(SubTask<LocalVersionInfo> task)
    {
        VersionInfoTask = task ?? throw new ArgumentNullException(nameof(task));
    }

    public void SetClientTask(SubTask<string?> task)
    {
        ClientTask = task ?? throw new ArgumentNullException(nameof(task));
    }

    public void SetLibrariesTask(SubTask<List<DownloadableFileInfo>> task)
    {
        LibrariesTask = task ?? throw new ArgumentNullException(nameof(task));
    }

    public void SetAssetsTask(SubTask<Dictionary<string, AssetInfo>> task)
    {
        AssetsTask = task ?? throw new ArgumentNullException(nameof(task));
    }

    public void SetFinalizationTask(SubTask<bool> task)
    {
        FinalizationTask = task ?? throw new ArgumentNullException(nameof(task));
    }

    public void SetFabricVersionJsonTask(SubTask<string> task)
    {
        FabricVersionJsonTask = task ?? throw new ArgumentNullException(nameof(task));
    }

    public void Register(string key, SubTaskBase task)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        _customTasks[key] = task ?? throw new ArgumentNullException(nameof(task));
    }

    public T GetRequired<T>(string key)
        where T : SubTaskBase
    {
        if (!TryGet(key, out T? task))
        {
            throw new KeyNotFoundException($"Task '{key}' has not been registered.");
        }

        return task!;
    }

    public bool TryGet<T>(string key, out T? task)
        where T : SubTaskBase
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        if (_customTasks.TryGetValue(key, out var rawTask) && rawTask is T typedTask)
        {
            task = typedTask;
            return true;
        }

        task = null;
        return false;
    }

    public IReadOnlyList<SubTaskBase> GetAllTasks()
    {
        var tasks = new List<SubTaskBase>();
        var taskIds = new HashSet<Guid>();

        AddTask(tasks, taskIds, VersionInfoTask);
        AddTask(tasks, taskIds, ClientTask);
        AddTask(tasks, taskIds, LibrariesTask);
        AddTask(tasks, taskIds, AssetsTask);
        AddTask(tasks, taskIds, FinalizationTask);
        AddTask(tasks, taskIds, FabricVersionJsonTask);

        foreach (var task in _customTasks.Values)
        {
            AddTask(tasks, taskIds, task);
        }

        return tasks;
    }

    private static void AddTask(ICollection<SubTaskBase> tasks, ISet<Guid> taskIds, SubTaskBase? task)
    {
        if (task == null || !taskIds.Add(task.Id))
        {
            return;
        }

        tasks.Add(task);
    }
}
