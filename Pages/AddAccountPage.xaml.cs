using iNKORE.UI.WPF.Controls;
using iNKORE.UI.WPF.Modern;
using iNKORE.UI.WPF.Modern.Controls;
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
            ChangeTheme(s_light);
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
            ContentDialog dialog = new ContentDialog();
            dialog.Title = "离线登录";
            SimpleStackPanel panel = new SimpleStackPanel();
            panel.Orientation = Orientation.Vertical;
            panel.Spacing = 5;
            panel.HorizontalAlignment = HorizontalAlignment.Center;
            panel.VerticalAlignment = VerticalAlignment.Center;
            TextBox box = new TextBox();
            box.HorizontalAlignment = HorizontalAlignment.Center;
            box.TextWrapping = TextWrapping.NoWrap;
            box.Width = 200;
            box.Height = 20;
            panel.Children.Add(box);
            Label label = new Label();
            label.Content = "请输入ID！";
            label.Foreground = new SolidColorBrush(Colors.Red);
            panel.Children.Add(label);
            SimpleStackPanel buttons = new SimpleStackPanel();
            buttons.Orientation = Orientation.Horizontal;
            buttons.HorizontalAlignment = HorizontalAlignment.Right;
            buttons.Spacing = 7;
            Button cancel = new Button();
            Button ok = new Button();
            ok.IsDefault = true;
            ok.UpdateDefaultStyle();
            ok.Content = "确定";
            cancel.Content = "取消";
            buttons.Children.Add(ok);
            buttons.Children.Add(cancel);
            panel.Children.Add(buttons);
            dialog.Content = panel;
            dialog.ShowAsync();
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            MainWindow.MainFrame.Navigate(MainWindow.AccountPage);
        }
    }
}
