using iNKORE.UI.WPF.Modern;
using LMC.Basic;
using LMC.Pages.AccountTypes;
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
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Page = iNKORE.UI.WPF.Modern.Controls.Page;

namespace LMC.Pages
{
    /// <summary>
    /// HomePage.xaml 的交互逻辑
    /// </summary>
    public partial class AddAccountPage : Page
    {
        private static bool s_light = true;
        private static List<Grid> s_grids = new List<Grid>();
        public MicrosoftLoginPage MicrosoftLoginPage = new MicrosoftLoginPage();

        public AddAccountPage()
        {
            s_light = !string.IsNullOrEmpty(Config.ReadGlobal("ui", "theme")) ? Config.ReadGlobal("ui", "theme") == "light" : ThemeManager.Current.ApplicationTheme == ApplicationTheme.Light;
            InitializeComponent();
            s_grids.Clear();
            s_grids.Add(offline);
            s_grids.Add(microsoft);
            this.Loaded += AddAccountPage_Loaded;
        }

        private void AddAccountPage_Loaded(object sender, RoutedEventArgs e)
        {
            foreach (Grid g in s_grids)
            {
                g.Background = s_light ? new SolidColorBrush(Colors.White) : new SolidColorBrush(Color.FromRgb(35, 39, 52));
                ((DropShadowEffect)g.Effect).Color = s_light ? Colors.Gray : Colors.White;
            }
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

        private void microsoft_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            MainWindow.MainFrame.Navigate(MicrosoftLoginPage);
        }

        private void offline_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {

        }
    }
}
