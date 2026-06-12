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
    public async Task CreateDownloadPlanAsync_DoesNotPrefetchVersionInfoBeforeTaskExecution()
    {
        var resolverCalls = 0;
        var request = new DownloadableGameVersion
        {
            RootPath = @"C:\Games\.minecraft",
            VersionId = "1.20.6",
            VersionName = "1.20.6",
            Loaders = []
        };
        var manager = new DownloadManager(
            DownloadSourceManager.CreateDefault(),
            [
                new FakeProvider(DownloadInstallationComponent.Vanilla, _ => true, _ => { }),
                CreateNoOpFinalizationProvider()
            ],
            (_, _) =>
            {
                Interlocked.Increment(ref resolverCalls);
                return Task.FromResult("""
                {
                  "id": "1.20.6",
                  "mainClass": "net.minecraft.client.main.Main",
                  "libraries": []
                }
                """);
            });

        var createPlanTask = manager.CreateDownloadPlanAsync(request);
        var completedTask = await Task.WhenAny(createPlanTask, Task.Delay(500));

        Assert.Same(createPlanTask, completedTask);

        var plan = await createPlanTask;
        Assert.Equal(0, resolverCalls);
        Assert.Equal(request, plan.Request);

        await WaitForConditionAsync(() => plan.ParentTask.SubTasks.All(task => task.IsFinished));
    }

    [Fact]
    public async Task CreateDownloadPlanAsync_RejectsUnsupportedLoaderType()
    {
        var request = new DownloadableGameVersion
        {
            RootPath = @"C:\Games\.minecraft",
            VersionId = "1.20.6",
            VersionName = "1.20.6-NeoForge",
            Loaders =
            [
                new ModLoader
                {
                    Type = ModLoaderType.NeoForge,
                    VersionId = "20.6.0"
                }
            ]
        };

        var manager = new DownloadManager();

        var exception = await Assert.ThrowsAsync<NotSupportedException>(() => manager.CreateDownloadPlanAsync(request));
        Assert.Contains("Unsupported", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    public void Dispose()
    {
        HttpUtils.ResetTransportForTesting();
    }

    private static IGameInstallationTaskProvider CreateNoOpFinalizationProvider()
    {
        return new FakeProvider(
            DownloadInstallationComponent.Finalization,
            _ => true,
            context =>
            {
                var task = context.ParentTask.CreateSubTask(
                    "noop-finalize",
                    int.MaxValue,
                    (_, _, progress) =>
                    {
                        progress.Report(100);
                        return Task.FromResult(true);
                    },
                    waitForSiblingTasksToComplete: true);
                context.Tasks.SetFinalizationTask(task);
            });
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

    private sealed class FakeProvider(
        DownloadInstallationComponent component,
        Func<DownloadInstallationContext, bool> shouldApply,
        Action<DownloadInstallationContext> addTasks)
        : IGameInstallationTaskProvider
    {
        public DownloadInstallationComponent Component { get; } = component;

        public bool ShouldApply(DownloadInstallationContext context) => shouldApply(context);

        public void AddTasks(DownloadInstallationContext context) => addTasks(context);
    }

}
