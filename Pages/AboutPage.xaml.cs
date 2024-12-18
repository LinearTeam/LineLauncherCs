﻿using iNKORE.UI.WPF.Controls;
using iNKORE.UI.WPF.Modern;
using iNKORE.UI.WPF.Modern.Controls;
using LMC.Basic;
using System;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Page = iNKORE.UI.WPF.Modern.Controls.Page;

namespace LMC.Pages
{
    /// <summary>
    /// HomePage.xaml 的交互逻辑
    /// </summary>
    public partial class AboutPage : Page
    {
        private static bool s_light = true;
        private static Expander s_aboutExpander;
        private static Border s_border;


        public AboutPage()
        {
            s_light = !string.IsNullOrEmpty(Config.ReadGlobal("ui", "theme")) ? Config.ReadGlobal("ui", "theme") == "light" : ThemeManager.Current.ApplicationTheme == ApplicationTheme.Light;
            InitializeComponent();
            s_aboutExpander = AboutExpander;
            s_border = BD;
            ChangeTheme(s_light);
            SizeChanged += AboutPage_SizeChanged;
            double width = this.Width;
            this.Width = 800;
            AboutPage_SizeChanged(null, null);
            Task.Run(async () => {
                await Task.Delay(20);
                this.Width = width;
                AboutPage_SizeChanged(null, null);
            });
        }


        public static void ChangeTheme(bool light)
        {
            s_light = light;
            s_border.Background = s_light ? new SolidColorBrush(Color.FromRgb(240, 243, 249)) : new SolidColorBrush(Color.FromRgb(29,32,39));
            s_aboutExpander.Background = s_light ? new SolidColorBrush(Color.FromRgb(240, 243, 249)) : new SolidColorBrush(Color.FromRgb(29, 32, 39));
        }

        private void AboutPage_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            try
            {
                CD.Width = new GridLength(AboutExpander.ActualWidth - SSP.ActualWidth - 30);
            }catch { }
        
        }

        private void AboutExpander_Collapsed(object sender, RoutedEventArgs e)
        {
            AboutExpander.Margin = new Thickness(0,0,20,0);
        }

        private void AboutExpander_Expanded(object sender, RoutedEventArgs e)
        {
            AboutExpander.Margin = new Thickness(0, 0, 20, 20);
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            Process.Start("explorer", "\"https://www.apache.org/licenses/LICENSE-2.0\"");
        }

        private void Button_Click_1(object sender, RoutedEventArgs e)
        {
            Process.Start("explorer", "\"https://www.github.com/LinearTeam/LineLauncherCs\"");
        }

        private async void Button_Click_2(object sender, RoutedEventArgs e)
        {
            ContentDialog dialog = new ContentDialog();
            dialog.Title = "检查更新";
            var content = new SimpleStackPanel();
            content.Orientation = Orientation.Vertical;
            content.Spacing = 20;
            var label = new Label();
            label.Content = "检查中...";
            var pb = new ProgressRing();
            content.Children.Add(label);
            content.Children.Add(pb);
            pb.IsIndeterminate = true;
            dialog.Content = content;
            dialog.ShowAsync();
            var ver = await Updater.Check();
            dialog.Title = "更新";
            if(ver == null)
            {
                dialog.CloseButtonText = "确认";
                dialog.Content = "检查失败！获取的版本内容为空！";
                return;
            }

            if(ver.Version == App.LauncherVersion && ver.Build == App.LauncherBuildVersion)
            {
                dialog.CloseButtonText = "确认";
                dialog.Content = "当前是最新版！";
                return;
            }

            if (!ver.SecurityOrEmergency)
            {
                dialog.Content = $"发现新的非紧急更新版！\n版本号：{ver.Version}\n类型：{ver.Type}\n构建号：{ver.Build}";
                dialog.CloseButtonText = "取消";
                dialog.PrimaryButtonText = "更新";
                dialog.DefaultButton = ContentDialogButton.Primary;
                dialog.PrimaryButtonClick += async (s, e) =>
                {
                    dialog.Content = content;
                    label.Content = "更新中...";
                    try
                    {
                        await Updater.Update(ver);
                    }
                    catch (Exception ex) {
                        new Logger("AP-UPD").Error($"更新失败：{ex.Message}\n{ex.StackTrace}");
                    }
                };
            }
            else
            {
                dialog.Content = $"发现新的紧急更新版！\n版本号：{ver.Version}\n类型：{ver.Type}\n构建号：{ver.Build}";
                dialog.PrimaryButtonText = "更新";
                dialog.DefaultButton = ContentDialogButton.Primary;
                dialog.PrimaryButtonClick += async (s, e) =>
                {
                    dialog.Content = content;
                    label.Content = "更新中...";
                    try
                    {
                        await Updater.Update(ver);
                    }
                    catch (Exception ex)
                    {
                        new Logger("AP-UPD").Error($"更新失败：{ex.Message}\n{ex.StackTrace}");
                    }
                };
            }
        }
    }
}
