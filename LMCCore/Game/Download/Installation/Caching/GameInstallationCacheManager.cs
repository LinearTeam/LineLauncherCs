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

using System.Collections.Concurrent;
using LMC;
using LMCCore.Tasks.Model;

namespace LMCCore.Game.Download.Installation.Caching;

public sealed class GameInstallationCacheManager
{
    private readonly ParentTask _parentTask;
    private readonly ConcurrentDictionary<Guid, SubTaskBase> _trackedTasks = [];
    private int _cleanupTriggered;

    public GameInstallationCacheManager(ParentTask parentTask)
    {
        _parentTask = parentTask ?? throw new ArgumentNullException(nameof(parentTask));
        CacheDirectory = Path.Combine(
            Current.LMCPath,
            "cache",
            "game-installations",
            _parentTask.Id.ToString("N"));
        CachedVersionJsonPath = Path.Combine(CacheDirectory, "version.json");
        CachedClientJarPath = Path.Combine(CacheDirectory, "client.jar");
    }

    public string CacheDirectory { get; }

    public string CachedVersionJsonPath { get; }

    public string CachedClientJarPath { get; }

    public void EnsureCacheDirectoryExists()
    {
        Directory.CreateDirectory(CacheDirectory);
    }

    public async Task CopyCacheToVersionDirectoryAsync(
        string rootPath,
        string versionName,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(versionName);

        cancellationToken.ThrowIfCancellationRequested();

        await CopyCachedFileToVersionDirectoryAsync(
            CachedVersionJsonPath,
            rootPath,
            versionName,
            $"{versionName}.json",
            required: true,
            cancellationToken);

        await CopyCachedFileToVersionDirectoryAsync(
            CachedClientJarPath,
            rootPath,
            versionName,
            $"{versionName}.jar",
            required: false,
            cancellationToken);
    }

    public Task CopyCachedFileToVersionDirectoryAsync(
        string sourcePath,
        string rootPath,
        string versionName,
        string relativePath,
        bool required,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(rootPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(versionName);
        ArgumentException.ThrowIfNullOrWhiteSpace(relativePath);

        var versionDirectory = Path.Combine(rootPath, "versions", versionName);
        var destinationPath = Path.Combine(versionDirectory, relativePath);
        var destinationDirectory = Path.GetDirectoryName(destinationPath)
                                   ?? throw new InvalidOperationException("Failed to resolve destination directory.");
        Directory.CreateDirectory(destinationDirectory);

        return CopyFileAsync(sourcePath, destinationPath, required, cancellationToken);
    }

    public void RegisterCleanupOnTaskCompletion(IEnumerable<SubTaskBase> tasks)
    {
        ArgumentNullException.ThrowIfNull(tasks);

        foreach (var task in tasks)
        {
            if (_trackedTasks.TryAdd(task.Id, task))
            {
                task.Completed += OnTrackedTaskCompleted;
            }
        }

        TryCleanupAfterAllTasksFinished();
    }

    public bool Cleanup()
    {
        try
        {
            if (Directory.Exists(CacheDirectory))
            {
                Directory.Delete(CacheDirectory, true);
            }

            return true;
        }
        catch
        {
            return false;
        }
    }

    private void OnTrackedTaskCompleted(SubTaskBase _)
    {
        TryCleanupAfterAllTasksFinished();
    }

    private void TryCleanupAfterAllTasksFinished()
    {
        if (_parentTask.SubTasks.Any(task => !task.IsFinished))
        {
            return;
        }

        if (Interlocked.Exchange(ref _cleanupTriggered, 1) == 1)
        {
            return;
        }

        foreach (var trackedTask in _trackedTasks.Values)
        {
            trackedTask.Completed -= OnTrackedTaskCompleted;
        }

        Cleanup();
    }

    async private static Task CopyFileAsync(
        string sourcePath,
        string destinationPath,
        bool required,
        CancellationToken cancellationToken)
    {
        if (!File.Exists(sourcePath))
        {
            if (required)
            {
                throw new InvalidOperationException($"Required cached file '{sourcePath}' was not found.");
            }

            return;
        }

        cancellationToken.ThrowIfCancellationRequested();

        await using var sourceStream = new FileStream(sourcePath, new FileStreamOptions
        {
            Access = FileAccess.Read,
            Mode = FileMode.Open,
            Share = FileShare.Read,
            Options = FileOptions.Asynchronous | FileOptions.SequentialScan
        });
        await using var destinationStream = new FileStream(destinationPath, new FileStreamOptions
        {
            Access = FileAccess.Write,
            Mode = FileMode.Create,
            Share = FileShare.None,
            Options = FileOptions.Asynchronous | FileOptions.SequentialScan
        });

        await sourceStream.CopyToAsync(destinationStream, cancellationToken);
    }
}
