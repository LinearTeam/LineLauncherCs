using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace LMC.Basic
{
    public class TaskManager
    {
        private static List<CustomTask> s_tasks = new List<CustomTask>();
        
        public static void AddTask(CustomTask task)
        {
            var runningTask = new List<CustomTask>();
            lock (s_tasks)
            {
                s_tasks.Add(task);
                foreach (var t in s_tasks)
                {
                    if (t.Running) runningTask.Add(t);
                }
            }

            if (runningTask.Count < 2)
            {
                RunTaskNow(task);
            }

        }

        public static void RunTaskNow(CustomTask task)
        {
            task.Running = true;
            Task.Run(async () =>
            {
                task.SubTasks.Sort(((subTask, task1) =>
                {
                    return subTask.Weight - task1.Weight;
                }));
                int lastWeight = -1;
                foreach (var subTask in task.SubTasks)
                {
                    await Task.Run(() => subTask.run(task));
                }
            });
        }
        
    }

    public class CustomTask
    {
        public bool Running { get; set; }
        private string _taskName;
        private int _weight;
        public readonly List<SubTask> SubTasks = new List<SubTask>();
        private readonly Dictionary<string, object> _values = new Dictionary<string, object>();

        public CustomTask(string taskName, int weight, SubTask init)
        {
            _taskName = taskName;
            _weight = weight;
            SubTasks.Add(init);
        }
    }

    public abstract class SubTask
    { 
        public string Name { get; }
        public int Weight { get; }
        public SubTask(string name, int weight)
        {
            Name = name;
            Weight = weight;
        }

        public abstract void run(CustomTask task);
    }
}