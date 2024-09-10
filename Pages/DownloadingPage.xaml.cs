using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Threading;
using Wpf.Ui.Controls;

namespace LMC.Pages
{
    /// <summary>
    /// DownloadingPage.xaml 的交互逻辑
    /// </summary>
    public partial class DownloadingPage : Page
    {
        public static System.Windows.Controls.TextBlock Processing;
        public static System.Windows.Controls.TextBlock ProcessingProgress;
        public static string ProProg;
        public static string ProProc;
        public static ProgressRing ProgressRing;
        private static DispatcherTimer s_timer = new DispatcherTimer();

        public DownloadingPage()
        {
            InitializeComponent();
            Processing = processing;
            ProcessingProgress = processing_progress;
            ProgressRing = ring;
            RefreshProc(null, null);
            this.Unloaded += WhenUnloaded;
            s_timer.Interval = TimeSpan.FromSeconds(0.5);
            s_timer.Tick += RefreshProc;
            s_timer.IsEnabled = true;
            s_timer.Start();
        }

        private void back_Click(object sender, RoutedEventArgs e)
        {
            MainWindow.MainNagView.Navigate(typeof(DownloadPage));
        }

        private void WhenUnloaded(object sender, EventArgs e)
        {
            s_timer.Stop();
            s_timer.IsEnabled = false;
        }

        private void RefreshProc(object sender, EventArgs e)
        {
            Processing.Text = ProProc;
            ProcessingProgress.Text = ProProg;
        }

    }
}
