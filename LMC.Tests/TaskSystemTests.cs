using LMCCore.Tasks;
using LMCCore.Tasks.Model;
using LMCUI.Pages.TaskPage;

namespace LMC.Tests;

public class TaskSystemTests
{
    [Fact]
    public async Task TaskManager_ExecutesDependenciesInOrder()
    {
        using var manager = new TaskManager(2);
        var executionOrder = new List<string>();
        manager.Start();

        var parent = manager.CreateParent("parent");
        var first = parent.CreateSubTask<object>("first", 0, async (_, _, _) =>
        {
            executionOrder.Add("first");
            await Task.Delay(30);
            return new object();
        });
        var second = parent.CreateSubTask<object>("second", 0, (_, _, _) =>
        {
            executionOrder.Add("second");
            return Task.FromResult(new object());
        }, [first]);

        await WaitForConditionAsync(() => second.State == TaskState.Completed);

        Assert.Equal(["first", "second"], executionOrder);
    }

    [Fact]
    public async Task TaskManager_RespectsMaxConcurrency()
    {
        using var manager = new TaskManager(1);
        var firstStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var secondStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseFirst = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var concurrentExecutions = 0;
        var maxConcurrentExecutions = 0;
        manager.Start();

        var parent = manager.CreateParent("parent");
        parent.CreateSubTask<object>("first", 0, async (_, _, _) =>
        {
            firstStarted.SetResult();
            maxConcurrentExecutions = Math.Max(maxConcurrentExecutions, Interlocked.Increment(ref concurrentExecutions));
            await releaseFirst.Task;
            Interlocked.Decrement(ref concurrentExecutions);
            return new object();
        });
        parent.CreateSubTask<object>("second", 0, async (_, _, _) =>
        {
            secondStarted.SetResult();
            maxConcurrentExecutions = Math.Max(maxConcurrentExecutions, Interlocked.Increment(ref concurrentExecutions));
            await Task.Delay(10);
            Interlocked.Decrement(ref concurrentExecutions);
            return new object();
        });

        await firstStarted.Task.WaitAsync(TimeSpan.FromSeconds(2));
        await Task.Delay(80);
        Assert.False(secondStarted.Task.IsCompleted);

        releaseFirst.SetResult();
        await secondStarted.Task.WaitAsync(TimeSpan.FromSeconds(2));
        await WaitForConditionAsync(() => parent.SubTasks.All(task => task.IsFinished));

        Assert.Equal(1, maxConcurrentExecutions);
    }

    [Fact]
    public async Task TaskManager_FaultsParentAndCancelsDependents()
    {
        using var manager = new TaskManager(2);
        manager.Start();

        var parent = manager.CreateParent("parent");
        var faulted = parent.CreateSubTask<object>("faulted", 0, (_, _, _) => throw new InvalidOperationException("boom"));
        var dependent = parent.CreateSubTask<object>("dependent", 0, (_, _, _) => Task.FromResult(new object()), [faulted]);

        await WaitForConditionAsync(() => parent.State == TaskState.Faulted && dependent.State == TaskState.Canceled);

        Assert.Equal(TaskState.Faulted, parent.State);
        Assert.Equal(TaskState.Faulted, faulted.State);
        Assert.Equal(TaskState.Canceled, dependent.State);
    }

    [Fact]
    public async Task TaskManager_StartAndStop_AreIdempotent()
    {
        using var manager = new TaskManager(1);

        manager.Start();
        manager.Start();

        await manager.StopAsync();
        await manager.StopAsync();
    }

    [Fact]
    public async Task TaskManager_CreateParentAutoStartsScheduler()
    {
        using var manager = new TaskManager(1);
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var parent = manager.CreateParent("parent");
        parent.CreateSubTask<object>("first", 0, (_, _, _) =>
        {
            started.SetResult();
            return Task.FromResult(new object());
        });

        await started.Task.WaitAsync(TimeSpan.FromSeconds(2));
        await WaitForConditionAsync(() => parent.SubTasks.All(task => task.State == TaskState.Completed));
    }

    [Fact]
    public void TaskPagePresentation_ComputesDisplayStateAndActionMode()
    {
        var parent = new ParentTask("parent");
        parent.SubTasks.Add(new TestSubTask(parent, TaskState.Completed));
        parent.SubTasks.Add(new TestSubTask(parent, TaskState.Faulted));

        var displayState = TaskPagePresentation.GetParentDisplayState(parent);
        var actionMode = TaskPagePresentation.GetParentActionButtonMode(displayState);

        Assert.Equal(TaskState.Faulted, displayState);
        Assert.Equal(ParentTaskActionButtonMode.Confirm, actionMode);
    }

    [Fact]
    public void TaskPagePresentation_DiffParents_TracksAddAndRemove()
    {
        var existing = new[]
        {
            new ParentTask("existing")
        };
        var added = new ParentTask("added");
        var diff = TaskPagePresentation.DiffParents(existing, [existing[0], added]);

        Assert.Single(diff.AddedParents);
        Assert.Empty(diff.RemovedParents);
        Assert.Equal(added.Id, diff.AddedParents[0].Id);
    }

    private static async Task WaitForConditionAsync(Func<bool> predicate, int timeoutMs = 3000)
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

    private sealed class TestSubTask : SubTaskBase
    {
        public TestSubTask(ParentTask parent, TaskState state, bool isExecuting = false) : base("test", 0, parent, null)
        {
            State = state;
            IsExecuting = isExecuting;
        }

        public override Task ExecuteAsync() => Task.CompletedTask;
    }
}
