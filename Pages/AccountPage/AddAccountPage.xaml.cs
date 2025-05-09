using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using iNKORE.UI.WPF.Controls;
using iNKORE.UI.WPF.Modern;
using iNKORE.UI.WPF.Modern.Controls;
using LMC.Account;
using LMC.Basic.Configs;
using LMC.Pages.AccountPage.AccountTypes;
using Page = iNKORE.UI.WPF.Modern.Controls.Page;

namespace LMC.Pages.AccountPage
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
            MainWindow.Navigate(MicrosoftLoginPage);
        }

        private static bool s_isValid = false;
        
        private void offline_MouseLeftButtonDown(object sender, MouseButtonEventArgs ignored)
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
            s_isValid = false;
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
            box.TextChanged += (s,e) =>
            {
                var text = box.Text;
                if (string.IsNullOrEmpty(text))
                {
                    label.Content = "请输入ID！";
                    s_isValid = false;
                    label.Foreground = new SolidColorBrush(Colors.Red);
                    return;
                }
                
                if (!Regex.IsMatch(text, @"^[a-zA-Z0-9_]+$"))
                {
                    label.Content = "ID仅允许数字、字母、下划线！";
                    s_isValid = false;
                    label.Foreground = new SolidColorBrush(Colors.Red);
                }
                else
                {
                    label.Content = "ID可用！";
                    s_isValid = true;
                    label.Foreground = new SolidColorBrush(Colors.LimeGreen);
                }
            };
            cancel.Click += (s, e) =>
            {
                dialog.Hide();
            };
            ok.Click += (s, e) =>
            {
                if(!s_isValid) return;
                dialog.Hide();
                string id = box.Text;
                Account.Model.Account account = new Account.Model.Account();
                account.Id = id;
                account.Type = AccountType.OFFLINE;
                AccountManager.AddAccount(account);
                MainWindow.ShowDialog("确认", "账号添加成功！","提示");
            };
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            MainWindow.Navigate(MainWindow.AccountPage);
        }
    }
}
