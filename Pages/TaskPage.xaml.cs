using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using iNKORE.UI.WPF.Modern.Common.IconKeys;
using iNKORE.UI.WPF.Modern.Controls;
using LMC.Tasks;
using Microsoft.Win32.SafeHandles;
using Page = iNKORE.UI.WPF.Modern.Controls.Page;

namespace LMC.Pages
{
    /// <summary>
    /// HomePage.xaml 的交互逻辑
    /// </summary>
    public partial class TaskPage : Page
    {
        public TaskManager TaskManager { get; set; }

        public TaskPage()
        {
            TaskManager = new TaskManager();
            TaskManager.PropertyChanged += (sender, args) => { RefreshTasks(); };
            this.Loaded += OnLoaded;
            InitializeComponent();
        }

        private static readonly List<int> s_handledTasks = new List<int>();

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            RefreshTasks();
        }

        public void RefreshTasks()
        {
            foreach (var task in TaskManager.Tasks)
            {
                if (s_handledTasks.Contains(task.Id)) continue;
                s_handledTasks.Add(task.Id);
                SettingsExpander se = new SettingsExpander();
                se.Header = task.TaskName;
                if (task.Status == ExecutionStatus.Running)
                {
                    se.IsExpanded = true;
                    ProgressRing ring = new ProgressRing();
                    se.HeaderIcon = ring;
                    ring.IsIndeterminate = true;
                }
                else if (task.Status == ExecutionStatus.Failed)
                {
                    se.IsExpanded = true;
                    FontIcon fontIcon = new FontIcon();
                    fontIcon.Icon = SegoeFluentIcons.Error;
                    se.HeaderIcon = fontIcon;
                }
                else if (task.Status == ExecutionStatus.Canceled) continue;
                else if (task.Status == ExecutionStatus.Completed) continue;
                else if (task.Status == ExecutionStatus.Uninitialized) continue;

                foreach (var sb in task.SubTasks)
                {
                    SettingsCard sc = new SettingsCard();
                    sc.Header = sb.SubTaskName;
                    if (sb.Status == ExecutionStatus.Running)
                    {
                        ProgressRing ring = new ProgressRing();
                        sc.HeaderIcon = ring;
                        ring.IsIndeterminate = true;
                    }
                    else if (sb.Status == ExecutionStatus.Failed)
                    {
                        FontIcon fontIcon = new FontIcon();
                        fontIcon.Icon = SegoeFluentIcons.Error;
                        sc.HeaderIcon = fontIcon;
                    }
                    else if (sb.Status == ExecutionStatus.Canceled) continue;
                    else if (sb.Status == ExecutionStatus.Completed)
                    {
                        FontIcon fontIcon = new FontIcon();
                        fontIcon.Icon = SegoeFluentIcons.Completed;
                        sc.HeaderIcon = fontIcon;
                    }

                    se.Items.Add(sc);
                    sb.StatusChanged += (o, args) =>
                    {
                        if (sb.Status == ExecutionStatus.Running)
                        {
                            ProgressRing ring = new ProgressRing();
                            sc.HeaderIcon = ring;
                            ring.IsIndeterminate = true;
                        }
                        else if (sb.Status == ExecutionStatus.Failed)
                        {
                            FontIcon fontIcon = new FontIcon();
                            fontIcon.Icon = SegoeFluentIcons.Error;
                            sc.HeaderIcon = fontIcon;
                            sc.Description = "执行失败：" + sb.ErrorMessage;
                        }
                        else if (sb.Status == ExecutionStatus.Completed)
                        {
                            FontIcon fontIcon = new FontIcon();
                            fontIcon.Icon = SegoeFluentIcons.Completed;
                            sc.HeaderIcon = fontIcon;
                        }
                    };

                }

                task.StatusChanged += (o, args) =>
                {
                    if (task.Status == ExecutionStatus.Running)
                    {
                        ProgressRing ring = new ProgressRing();
                        se.HeaderIcon = ring;
                        ring.IsIndeterminate = true;
                    }
                    else if (task.Status == ExecutionStatus.Failed)
                    {
                        FontIcon fontIcon = new FontIcon();
                        fontIcon.Icon = SegoeFluentIcons.Error;
                        se.HeaderIcon = fontIcon;
                    }
                    else if (task.Status == ExecutionStatus.Completed) ssp.Children.Remove(se);
                };
                Button cancel = new Button();
                cancel.Content = "取消";
                se.Content = cancel;
                cancel.Click += (o, args) =>
                {
                    if (task.Status != ExecutionStatus.Canceled) task.Cancel();
                    ssp.Children.Remove(se);
                };
                ssp.Children.Add(se);
            }
        }
    }
}
