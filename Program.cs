namespace TaskManagerTest;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

class TaskManager
{
    // 限制并发执行的最大任务数
    private readonly SemaphoreSlim _concurrencyLimit;
    
    // 存储所有任务
    private readonly List<TaskItem> _tasks = new List<TaskItem>();
    
    // 存储正在执行的任务和子任务
    private readonly List<TaskItem> _executingTasks = new List<TaskItem>();

    public TaskManager(int maxConcurrentTasks)
    {
        _concurrencyLimit = new SemaphoreSlim(maxConcurrentTasks);
    }

    public void AddTask(TaskItem task)
    {
        lock (_tasks)
        {
            _tasks.Add(task);
        }
        ExecuteTaskAsync(task);
    }

    public void RemoveTask(TaskItem task)
    {
        lock (_tasks)
        {
            _tasks.Remove(task);
        }
    }

    private async Task ExecuteTaskAsync(TaskItem task)
    {
        // 等待可用的执行槽
        await _concurrencyLimit.WaitAsync();

        try
        {
            // 将任务标记为正在执行
            lock (_executingTasks)
            {
                _executingTasks.Add(task);
            }

            // 执行子任务，按优先级排序
            foreach (var subTask in task.SubTasks.OrderBy(st => st.Priority))
            {
                await ExecuteSubTaskAsync(subTask);
            }
        }
        finally
        {
            // 执行完毕，释放执行槽
            _concurrencyLimit.Release();

            // 完成任务时，从正在执行的任务列表中移除
            lock (_executingTasks)
            {
                _executingTasks.Remove(task);
            }
        }
    }

    private async Task ExecuteSubTaskAsync(SubTask subTask)
    {
        Console.WriteLine($"Executing sub-task with priority {subTask.Priority}...");
        await Task.Delay(1000);
        Task.Run(async () =>
        {
            await Task.Run(subTask.Action);
            subTask.Success = true;
        });
    }

    public string GetExecutingStatus()
    {
        lock (_executingTasks)
        {
            var taskStatuses = _executingTasks.Select(task =>
                $"Task '{task.Name}' - SubTasks: {string.Join(", ", task.SubTasks.Select(st => $"Name {st.Name}, Priority {st.Priority}, Done {st.Success}"))}"
            );
            return string.Join(Environment.NewLine, taskStatuses);
        }
    }
}

class TaskItem
{
    public string Name { get; }
    public List<SubTask> SubTasks { get; }

    public TaskItem(string name)
    {
        Name = name;
        SubTasks = new List<SubTask>();
    }

    public void AddSubTask(SubTask subTask)
    {
        SubTasks.Add(subTask);
    }
}

class SubTask
{
    public string Name { get; }
    public int Priority { get; }
    public Action Action { get; }
    public bool Success { get; set; }

    public SubTask(string name, int priority, Action action)
    {
        Name = name;
        Priority = priority;
        Action = action;
        Success = false;
    }
}

public class Program
{
    static async Task Main(string[] args)
    {
        var taskManager = new TaskManager(2);  // 最大同时执行2个任务

        var task1 = new TaskItem("Task 1");
        task1.AddSubTask(new SubTask("T1",1, async () =>
        {
            await Task.Delay(10000);
            Console.WriteLine("Task 1 SubTask 1 executed");
        }));
        task1.AddSubTask(new SubTask("T2",2, () => Console.WriteLine("Task 1 SubTask 2 executed")));

        var task2 = new TaskItem("Task 2");
        task2.AddSubTask(new SubTask("T3",1, async () =>
        {
            await Task.Delay(5000);
            Console.WriteLine("Task 2 SubTask 1 executed");
        }));

        task2.AddSubTask(new SubTask("T4",1, async () =>
        {
            await Task.Delay(20000);
            Console.WriteLine("Task 2 SubTask 2 executed");
        }));
        task2.AddSubTask(new SubTask("T5",5, () => Console.WriteLine("Task 2 SubTask 3 executed")));

        taskManager.AddTask(task1);
        taskManager.AddTask(task2);

        Console.WriteLine("Executing Tasks...");
        while (true)
        {
            Console.WriteLine("Current Executing Tasks:");
            Console.WriteLine(taskManager.GetExecutingStatus());
            Console.Clear();
        }
    }
}
