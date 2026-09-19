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
using System.Net;
using System.Text.Json;
using LMCCore.Game.Download;
using LMCCore.Game.Download.Installation;
using LMCCore.Game.Download.Installation.Caching;
using LMCCore.Game.Download.Installation.Providers;
using LMCCore.Game.Download.Model;
using LMCCore.Game.Model.Loaders;
using LMCCore.Tasks.Model;
using LMCCore.Utils;

namespace LineLauncherCs.Tests;

public class DownloadManagerTests : IDisposable
{
    [Fact]
    public async Task GameInstallationCacheManager_CleansCacheAfterAllSubTasksFinishSuccessfully()
    {
        using var scope = new TestFileSystemScope();
        var parent = new ParentTask("parent");
        var cacheManager = new GameInstallationCacheManager(parent);
        cacheManager.EnsureCacheDirectoryExists();
        await File.WriteAllTextAsync(cacheManager.CachedVersionJsonPath, "{}");

        parent.CreateSubTask<object>("first", 0, async (_, _, progress) =>
        {
            await Task.Delay(20);
            progress.Report(100);
            return new object();
        });
        parent.CreateSubTask<object>("second", 1, async (_, _, progress) =>
        {
            await Task.Delay(20);
            progress.Report(100);
            return new object();
        });

        cacheManager.RegisterCleanupOnTaskCompletion(parent.SubTasks);

        await WaitForConditionAsync(() => parent.SubTasks.All(task => task.IsFinished));
        await WaitForConditionAsync(() => !Directory.Exists(cacheManager.CacheDirectory));
    }

    [Fact]
    public async Task GameInstallationCacheManager_CleansCacheAfterFailureEvenWhenTailTaskDoesNotRun()
    {
        using var scope = new TestFileSystemScope();
        var parent = new ParentTask("parent");
        var cacheManager = new GameInstallationCacheManager(parent);
        cacheManager.EnsureCacheDirectoryExists();
        await File.WriteAllTextAsync(cacheManager.CachedVersionJsonPath, "{}");

        var failingTask = parent.CreateSubTask<object>("failing", 0, (_, _, _) => throw new InvalidOperationException("boom"));
        var tailTask = parent.CreateSubTask<object>(
            "tail",
            int.MaxValue,
            (_, _, _) => Task.FromResult(new object()),
            waitForSiblingTasksToComplete: true);

        cacheManager.RegisterCleanupOnTaskCompletion(parent.SubTasks);

        await WaitForConditionAsync(() => parent.SubTasks.All(task => task.IsFinished));
        await WaitForConditionAsync(() => !Directory.Exists(cacheManager.CacheDirectory));

        Assert.Equal(TaskState.Faulted, failingTask.State);
        Assert.Equal(TaskState.Canceled, tailTask.State);
    }

    [Fact]
    public async Task GameInstallationCacheManager_CopyCachedFileToVersionDirectoryAsync_CopiesFileIntoVersionSubDirectory()
    {
        using var scope = new TestFileSystemScope();
        var parent = new ParentTask("parent");
        var cacheManager = new GameInstallationCacheManager(parent);
        cacheManager.EnsureCacheDirectoryExists();

        var cachedModsDirectory = Path.Combine(cacheManager.CacheDirectory, "mods");
        Directory.CreateDirectory(cachedModsDirectory);
        var cachedModPath = Path.Combine(cachedModsDirectory, "OptiFine-installer.jar");
        await File.WriteAllTextAsync(cachedModPath, "installer");

        var rootPath = Path.Combine(scope.RootPath, ".minecraft");
        await cacheManager.CopyCachedFileToVersionDirectoryAsync(
            cachedModPath,
            rootPath,
            "1.21.9-Fabric",
            Path.Combine("mods", "OptiFine-installer.jar"),
            required: true,
            CancellationToken.None);

        var versionModPath = Path.Combine(
            rootPath,
            "versions",
            "1.21.9-Fabric",
            "mods",
            "OptiFine-installer.jar");

        Assert.True(File.Exists(versionModPath));
        Assert.Equal("installer", await File.ReadAllTextAsync(versionModPath));
    }

    public void Dispose()
    {
        HttpUtils.ResetTransportForTesting();
    }

    async private static Task WaitForConditionAsync(Func<bool> predicate, int timeoutMs = 3000)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        while (!predicate())
        {
            if (DateTime.UtcNow >= deadline)
            {
                throw new TimeoutException("Condition was not met within the expected time.");
            }

            await Task.Delay(20);
        }
    }

}
