using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using iNKORE.UI.WPF.Controls;
using iNKORE.UI.WPF.Modern.Common.IconKeys;
using iNKORE.UI.WPF.Modern.Controls;
using LMC.Account;
using LMC.Basic;
using LMC.Basic.Configs;
using LMC.Basic.Model;
using LMC.Minecraft;
using LMC.Minecraft.Profile;
using Page = iNKORE.UI.WPF.Modern.Controls.Page;

namespace LMC.Pages.SettingsPage
{
    public class JavaItem
    {
        public Java Java { get; set; }
        public object Icon { get; set; }
    }
    /// <summary>
    /// HomePage.xaml 的交互逻辑
    /// </summary>
    public partial class JavaPage : Page
    {
        public List<JavaItem> Items { get; set; } = new List<JavaItem>();
        public JavaPage()
        {
            InitializeComponent();
            Loaded += OnLoaded;
        }

        private async void OnLoaded(object sender, RoutedEventArgs e)
        {
            jse.Items = Items;
            if (JavaManager.SearchingJava)
            {
                ContentDialog dialog = new ContentDialog();
                dialog.Title = "Java 管理";
                var content = new SimpleStackPanel();
                content.Orientation = Orientation.Vertical;
                content.Spacing = 5;
                ProgressRing ring = new ProgressRing();
                ring.IsIndeterminate = true;
                content.Children.Add(ring);
                content.Children.Add(new Label() { Content = "正在加载Java，如用时过长可在 设置 - 启动器设置 - Java深度 中适当减小搜索深度。"});
                dialog.Content = content;
                dialog.ShowAsync();
                DispatcherTimer timer = new DispatcherTimer();
                timer.Interval = TimeSpan.FromSeconds(2);
                timer.Tick += async (s, e) =>
                {
                    if (!JavaManager.SearchingJava)
                    {
                        timer.Stop();
                        timer.IsEnabled = false;
                        await Task.Run(() => RefreshJavas()).ConfigureAwait(false);
                        Dispatcher.Invoke(() => dialog.Hide());
                    }
                };
                timer.Start();
            }
            RefreshJavas();
        }

        public void RefreshJavas()
        {
            var javas = JavaManager.GetJavaList();
            
            Dispatcher.Invoke(() =>
            {
                var tempList = new List<JavaItem>();
                javas.ForEach(java =>
                {
                    JavaItem item = new JavaItem();
                    item.Java = java;
                    try
                    {
                        var icon = GetExeIcon(java.Path);
                        var image = new Image();
                        image.Source = icon;
                        item.Icon = image;
                    }
                    catch (Exception ex)
                    {
                        new Logger("JP").Error("获取Java图标时出错：" + ex);
                        item.Icon = new FontIcon(SegoeFluentIcons.OpenFile);
                    }
                    tempList.Add(item);
                });
                Items = tempList;
                jse.ItemsSource = Items;
            });
        }
        
        public static ImageSource GetExeIcon(string exePath)
        {
            var icon = System.Drawing.Icon.ExtractAssociatedIcon(exePath);
            return System.Windows.Interop.Imaging.CreateBitmapSourceFromHIcon(
                icon.Handle,
                new Int32Rect(0, 0, icon.Width, icon.Height),
                BitmapSizeOptions.FromEmptyOptions());
        }
        
        private void ButtonBase_OnClick(object sender, RoutedEventArgs e)
        {
            MainWindow.Navigate(MainWindow.SettingPage);
        }

        private void Search_Click(object sender, RoutedEventArgs e)
        {
            ContentDialog dialog = new ContentDialog();
            dialog.Title = "Java 管理";
            var content = new SimpleStackPanel();
            content.Orientation = Orientation.Vertical;
            content.Spacing = 5;
            ProgressRing ring = new ProgressRing();
            ring.IsIndeterminate = true;
            content.Children.Add(ring);
            content.Children.Add(new TextBlock() { Text = "正在加载Java，如用时过长可在 设置 - 启动器设置 - Java深度 中适当减小搜索深度。", TextWrapping = TextWrapping.Wrap });
            dialog.Content = content;
            dialog.CloseButtonText = "取消";
            dialog.ShowAsync();
            DispatcherTimer timer = new DispatcherTimer();
            CancellationTokenSource source = new CancellationTokenSource();
            dialog.Closed += (s, e) => source.Cancel();
            Task.Run(() => JavaManager.SearchJava(Config.ReadGlobal("java", "depth") == null ? 4 : int.Parse(Config.ReadGlobal("java", "depth")), source)).ConfigureAwait(false);
            timer.Interval = TimeSpan.FromMilliseconds(200);
            timer.Tick += async (s, e) =>
            {
                if (!JavaManager.SearchingJava)
                {
                    timer.Stop();
                    timer.IsEnabled = false;
                    Task.Run(() => RefreshJavas()).ConfigureAwait(false);
                    if(!source.IsCancellationRequested) Dispatcher.Invoke(() => dialog.Hide());
                }
            };
            timer.Start();
        }
    }
}
