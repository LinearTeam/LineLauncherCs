using System.Collections.Generic;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using iNKORE.UI.WPF.Modern;
using LMC.Basic.Configs;
using LMC.Pages.DownloadPage.DownloadTypes;
using Page = iNKORE.UI.WPF.Modern.Controls.Page;

namespace LMC.Pages.DownloadPage
{
    /// <summary>
    ///     HomePage.xaml 的交互逻辑
    /// </summary>
    public partial class DownloadPage : Page
    {
        private static bool s_light = true;
        private static List<Grid> s_grids = new List<Grid>();

        public DownloadPage()
        {
            s_light = !string.IsNullOrEmpty(Config.ReadGlobal("ui", "theme")) ? Config.ReadGlobal("ui", "theme") == "light" : ThemeManager.Current.ApplicationTheme == ApplicationTheme.Light;
            InitializeComponent();
            s_grids.Clear();
            s_grids.Add(minecraft);
            ChangeTheme(s_light);
        }

        
        public static void ChangeTheme(bool light)
        {
            s_light = light;
            foreach (Grid g in s_grids)
            {
                g.Background = s_light ? new SolidColorBrush(Colors.White) : new SolidColorBrush(Color.FromRgb(35, 39, 52));
                ((DropShadowEffect)g.Effect).Color = s_light ? Colors.Gray : Colors.White;
            }
        }

        public static readonly DownloadMinecraftPage DownloadMinecraftPage = new DownloadMinecraftPage();
        
        private void Minecraft_OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            MainWindow.Navigate(DownloadMinecraftPage);
        }

    }
}