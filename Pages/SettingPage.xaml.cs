using System;
using System.IO;
using System.Windows;
using iNKORE.UI.WPF.Modern;
using iNKORE.UI.WPF.Modern.Controls;
using LMC.Basic;
using Microsoft.Win32;
using Page = iNKORE.UI.WPF.Modern.Controls.Page;

namespace LMC.Pages
{
    /// <summary>
    /// HomePage.xaml 的交互逻辑
    /// </summary>
    public partial class SettingPage : Page
    {
        private static ToggleSwitch s_theme;
        public SettingPage()
        {
            InitializeComponent();
            s_theme = Theme;
            LoadSettings();
        }

        public static void LoadSettings()
        {
            while (true)
            {
                try
                {
                    s_theme.IsOn = !string.IsNullOrEmpty(Config.ReadGlobal("ui", "theme")) ? Config.ReadGlobal("ui", "theme") == "light" : ThemeManager.Current.ApplicationTheme == ApplicationTheme.Light;
                    break;
                }
                catch { }
            }
        }

        private void ToggleSwitch_Toggled(object sender, RoutedEventArgs e)
        {
            ThemeManager.Current.ApplicationTheme = Theme.IsOn ? ApplicationTheme.Light : ApplicationTheme.Dark;
            Config.WriteGlobal("ui", "theme", Theme.IsOn ? "light" : "dark");
        }

        private async void Button_Click_1(object sender, RoutedEventArgs e)
        {
            var a = await Secrets.Export("用户手动操作");
            System.Diagnostics.Process.Start("explorer", "/select," + a.Replace("/", "\\"));
        }

        private async void Button_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog ofd = new OpenFileDialog();
            ofd.CheckFileExists = true;
            ofd.Title = "选择文件";
            ofd.CheckPathExists = true;
            ofd.Multiselect = false;
            ofd.DefaultExt = Directory.GetParent("./LMC").FullName;
            if (ofd.ShowDialog() == true)
            {
                try {
                    Secrets.Import(ofd.FileName);
                    await MainWindow.ShowDialog("确定", "导入成功", "提示", ContentDialogButton.Close);
                }
                catch (Exception ex)
                {
                    await MainWindow.ShowDialog("确定", $"导入失败，原因：\n{ex.Message}\n{ex.StackTrace}", "提示", ContentDialogButton.Close);
                }
            }
        }
    }
}
