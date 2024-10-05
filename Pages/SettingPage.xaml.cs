using System;
using System.IO;
using System.Windows;
using iNKORE.UI.WPF.Modern;
using iNKORE.UI.WPF.Modern.Controls;
using iNKORE.UI.WPF.Modern.Controls.Helpers;
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
            AccountPage.ChangeTheme(Theme.IsOn);
        }

        private async void Button_Click_1(object sender, RoutedEventArgs e)
        {
            var res = await MainWindow.ShowDialog("取消", "导出后的文件是被解密(使用固定密钥加密)的，并不安全，他人获取后可以获得你的正版账号等信息，请不要随意发送给他人。","警告", ContentDialogButton.Close, "继续");
            if (res == ContentDialogResult.None) { return; }
            var a = await Secrets.Export("用户手动操作");
            System.Diagnostics.Process.Start("explorer", "/select," + a.Replace("/", "\\"));
        }

        private async void Button_Click(object sender, RoutedEventArgs e)
        {
            var res = await MainWindow.ShowDialog("取消", "接下来请选择以 .linesec 结尾的 LMC 隐私文件，随后会导入所选文件中记录的内容(如账号)，这会将已有名称相同、类型相同的账号覆盖，当前不存在的则不会覆盖，是否继续？", "提示", ContentDialogButton.Primary, "继续");
            if (res == ContentDialogResult.None) { return; }

            OpenFileDialog ofd = new OpenFileDialog();
            ofd.CheckFileExists = true;
            ofd.Title = "选择文件";
            ofd.CheckPathExists = true;
            ofd.Multiselect = false;
            ofd.Filter = "Line隐私文件包|*.linesec";
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
