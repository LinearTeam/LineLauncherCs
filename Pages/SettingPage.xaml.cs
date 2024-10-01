using System.Windows;
using iNKORE.UI.WPF.Modern;
using iNKORE.UI.WPF.Modern.Controls;
using LMC.Basic;
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
        }

        public static void LoadSettings()
        {
            s_theme.IsOn = !string.IsNullOrEmpty(Config.ReadGlobal("ui", "theme")) ? Config.ReadGlobal("ui", "theme") == "light" : ThemeManager.Current.ApplicationTheme == ApplicationTheme.Light;
        }

        private void ToggleSwitch_Toggled(object sender, RoutedEventArgs e)
        {
            ThemeManager.Current.ApplicationTheme = Theme.IsOn ? ApplicationTheme.Light : ApplicationTheme.Dark;
            Config.WriteGlobal("ui", "theme", Theme.IsOn ? "light" : "dark");
        }

        private async void Button_Click_1(object sender, RoutedEventArgs e)
        {
            var a = await Secrets.Export("1");
            System.Diagnostics.Process.Start("explorer", "/select," + a.Replace("/", "\\"));
        }
    }
}
