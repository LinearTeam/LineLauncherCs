using System;
using System.Collections.Generic;

namespace LMC.Basic
{
    public class TaskManager
    {
        
    }

    public class CustomTask
    {
        private string _taskName;
        private int _weight;
        private List<SubTask> _tasks;
        private Dictionary<string, object> _values;
    }

    public class SubTask
    {
        private string _taskName;
        private int _weight;
        private Action<bool> _action;
    }
}