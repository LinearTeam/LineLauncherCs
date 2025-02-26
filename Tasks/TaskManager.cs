using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using iNKORE.UI.WPF.Modern.Controls;
using LMC.Basic;

namespace LMC.Tasks
{
    public enum ExecutionStatus
    {
        Waiting,
        Running,
        Completed,
        Failed,
        Canceled,
        Uninitialized
    }

    public abstract class TaskBase : INotifyPropertyChanged
    {
        // ReSharper disable once InconsistentNaming
        protected readonly Logger _logger;
        private ExecutionStatus _status;
        private string _errorMessage;

        public int Id { get; }
        public int Weight { get; }
        public CancellationTokenSource CancellationTokenSource { get; } = new CancellationTokenSource();

        public ExecutionStatus Status
        {
            get => _status;
            set
            {
                _status = value;
                OnPropertyChanged(nameof(Status));
                OnStatusChanged();
            }
        }

        public string ErrorMessage
        {
            get => _errorMessage;
            protected set
            {
                _errorMessage = value;
                OnPropertyChanged(nameof(ErrorMessage));
            }
        }

        public event EventHandler StatusChanged;
        public event PropertyChangedEventHandler PropertyChanged;

        protected TaskBase(int id, int weight)
        {
            Id = id;
            Weight = weight;
            var typeName = this is TaskItem ? "任务" : "子任务";
            _logger = new Logger($"{typeName}-{id}");
            Status = ExecutionStatus.Uninitialized;
        }

        protected virtual void OnStatusChanged() => StatusChanged?.Invoke(this, EventArgs.Empty);
        protected virtual void OnPropertyChanged(string propertyName) => 
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

        public void Cancel()
        {
            if (!CancellationTokenSource.IsCancellationRequested)
            {
                if (Status != ExecutionStatus.Completed || Status != ExecutionStatus.Failed)
                {
                    CancellationTokenSource.Cancel();
                    _logger.Warn($"{GetTaskTypeName()} {Id} 已收到取消请求。");
                    Status = ExecutionStatus.Canceled;
                }
            }
        }

        private string GetTaskTypeName() => this is TaskItem ? "任务" : "子任务";
    }

    public class SubTask : TaskBase
    {
        public Func<CancellationToken, Task> Action { get; }
        public int ParentTaskId { get; }
        public string SubTaskName { get; }
        public SubTask(int id, int parentTaskId, int weight, Func<CancellationToken, Task> action, Logger parentLogger, string subTaskName) 
            : base(id, weight)
        {
            ParentTaskId = parentTaskId;
            Action = action;
            SubTaskName = subTaskName;
        }

        public async Task ExecuteAsync()
        {
            try
            {
                if(CancellationTokenSource.IsCancellationRequested) return;
                Status = ExecutionStatus.Running;
                _logger.Info($"子任务 {Id} 已启动。");

                await Action(CancellationTokenSource.Token);

                if (CancellationTokenSource.IsCancellationRequested)
                {
                    Status = ExecutionStatus.Canceled;
                    
                    _logger.Warn($"子任务 {Id} 在执行过程中被取消。");
                }
                else
                {
                    Status = ExecutionStatus.Completed;
                    _logger.Info($"子任务 {Id} 已完成。");
                }
            }
            catch (OperationCanceledException)
            {
                Status = ExecutionStatus.Canceled;
                _logger.Warn($"子任务 {Id} 已被取消。");
            }
            catch (Exception ex)
            {
                ErrorMessage = ex.Message;
                Status = ExecutionStatus.Failed;
                _logger.Error($"子任务 {Id} 执行失败：{ex.Message}" + "\n" + ex.StackTrace);
            }
        }
    }

    public class TaskItem : TaskBase
    {
        public ObservableCollection<SubTask> SubTasks { get; } = new ObservableCollection<SubTask>();
        public string TaskName { get; }
        public TaskItem(int id, int weight, Logger parentLogger, string name) 
            : base(id, weight)
        {
            TaskName = name;
            SubTasks.CollectionChanged += (s, e) => OnPropertyChanged(nameof(SubTasks));
        }
        

    }

    public class TaskManager : INotifyPropertyChanged
    {
        public static readonly TaskManager Instance = new TaskManager();
        public static readonly Dictionary<int, Dictionary<string, object>> Values = new Dictionary<int, Dictionary<string, object>>();
        private readonly Logger _logger = new Logger("TMG");
        private int _taskIdCounter = 1;
        private int _subTaskIdCounter = 1;
        
        private readonly ConcurrentDictionary<int, TaskItem> _tasks = new ConcurrentDictionary<int, TaskItem>();

        public ObservableCollection<TaskItem> Tasks { get; } = new ObservableCollection<TaskItem>();
        public event EventHandler<TaskStatusChangedEventArgs> GlobalStatusChanged;

        public TaskItem CreateTask(int weight, string name)
        {
            var task = new TaskItem(_taskIdCounter++, weight, _logger, name);
            _tasks.TryAdd(task.Id, task);
            Application.Current.Dispatcher.Invoke(() => Tasks.Add(task));
            _logger.Info($"已创建新任务 {task.Id}，权重：{weight}, 名称 {task.TaskName}");
            return task;
        }

        public SubTask AddSubTask(int parentTaskId, int weight, Func<CancellationToken, Task> action, string name)
        {
            if (!_tasks.TryGetValue(parentTaskId, out var parentTask))
            {
                _logger.Error($"添加子任务失败：找不到父任务 {parentTaskId}");
                return null;
            }

            var subTask = new SubTask(_subTaskIdCounter++, parentTaskId, weight, action, _logger, name);
            Application.Current.Dispatcher.Invoke(() => parentTask.SubTasks.Add(subTask));
            _logger.Info($"任务 {parentTaskId} 已添加子任务  {subTask.SubTaskName} ({subTask.Id})");
            return subTask;
        }

        public void CancelAllTasks()
        {
            foreach (var task in _tasks.Values)
            {
                task.Cancel();
                foreach (var subTask in task.SubTasks)
                {
                    subTask.Cancel();
                }
            }
            _logger.Info("已发送全局取消请求。");
        }

        public async Task ExecuteTasksAsync()
        {
            _logger.Info("开始执行任务队列。");
            if (PropertyChanged != null)
            {
                this.PropertyChanged.Invoke(this, null);
            }

            try
            {
                var taskGroups = _tasks.Values
                    .GroupBy(t => t.Weight)
                    .OrderBy(g => g.Key);

                foreach (var group in taskGroups)
                {
                    var tasks = group.Select(async task =>
                    {
                        if (task.CancellationTokenSource.IsCancellationRequested)
                        {
                            _logger.Warn($"任务 {task.Id} 在开始执行前已被取消。");
                            return;
                        }
                        
                        if(task.Status != ExecutionStatus.Waiting) return;
                        
                        task.Status = ExecutionStatus.Running;
                        _logger.Info($"任务 {task.Id} 开始执行。");
                        MainWindow.ChangeTaskInfoBadge(1);

                        await ExecuteSubTasksAsync(task);

                        task.Status = task.SubTasks.All(st => 
                            st.Status == ExecutionStatus.Completed) 
                            ? ExecutionStatus.Completed 
                            : ExecutionStatus.Failed;

                        _logger.Info($"任务 {task.Id} 执行结束，最终状态：{task.Status}");
                        if (task.Status == ExecutionStatus.Completed)
                        {
                            MainWindow.Instance.EnqueueMessage(new InfoBarMessage($"任务 {task.TaskName} 执行完成！", InfoBarSeverity.Success, "任务管理"));
                        }
                        else
                        {
                            MainWindow.Instance.EnqueueMessage(new InfoBarMessage($"任务 {task.TaskName} 执行失败！", InfoBarSeverity.Error, "任务管理"));
                        }
                        _tasks.TryRemove(task.Id, out _);
                        MainWindow.ChangeTaskInfoBadge(-1);
                    });

                    await Task.WhenAll(tasks);
                }
            }
            catch (Exception ex)
            {
                _logger.Error("任务执行队列发生异常。" + ex.Message + "\n" + ex.StackTrace);
                if (Logger.DebugMode)
                {
                    App.ShowException(ex);
                }
                else MainWindow.ShowDialog("确认","任务队列执行出错，请打开调试模式获取详细信息。","错误");
            }
        }

        private async Task ExecuteSubTasksAsync(TaskItem task)
        {
            var subTaskGroups = task.SubTasks
                .GroupBy(st => st.Weight)
                .OrderBy(g => g.Key);

            foreach (var group in subTaskGroups)
            {
                foreach (var subTask in group)
                {
                    if (!subTask.CancellationTokenSource.IsCancellationRequested && !task.CancellationTokenSource.IsCancellationRequested)
                    {
                        await subTask.ExecuteAsync();
                        if (subTask.Status == ExecutionStatus.Failed){ 
                            task.Status = ExecutionStatus.Failed;
                            return;
                        }
                    }
                    else
                    {
                        task.Status = ExecutionStatus.Canceled;
                        subTask.CancellationTokenSource.Cancel();
                    } 
                }
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual void OnPropertyChanged(string propertyName) => 
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    public class TaskStatusChangedEventArgs : EventArgs
    {
        public TaskBase ChangedTask { get; }
        public bool IsSubTask { get; }

        public TaskStatusChangedEventArgs(TaskBase task, bool isSubTask)
        {
            ChangedTask = task;
            IsSubTask = isSubTask;
        }
    }
}
