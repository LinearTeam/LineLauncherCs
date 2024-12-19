using iNKORE.UI.WPF.Modern;
using iNKORE.UI.WPF.Modern.Common.IconKeys;
using iNKORE.UI.WPF.Modern.Controls;
using LMC.Account;
using LMC.Basic;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using Page = iNKORE.UI.WPF.Modern.Controls.Page;

namespace LMC.Pages
{
    /// <summary>
    /// HomePage.xaml 的交互逻辑
    /// </summary>
    public partial class AccountPage : Page
    {
        private static bool s_light = true;
        private static WrapPanel s_stackPanel;
        private static List<Grid> s_grids = new List<Grid>();
        private static Grid s_mainGrid;
        private static Grid s_addAccount = new Grid();
        private static List<string> s_addedAccounts = new List<string>();
        private static Style s_triggerStyle;


        public AccountPage()
        {
            s_light = !string.IsNullOrEmpty(Config.ReadGlobal("ui", "theme")) ? Config.ReadGlobal("ui", "theme") == "light" : ThemeManager.Current.ApplicationTheme == ApplicationTheme.Light;
            InitializeComponent();
            s_mainGrid = MainGrid;
            s_stackPanel = ssp;
            s_triggerStyle = (Style)MainGrid.FindResource("TriggerStyle");
            AddGrid();
            this.Loaded += AccountPage_Loaded;
            ChangeTheme(s_light);
        }

        private async void AccountPage_Loaded(object sender, RoutedEventArgs e)
        {
            await RefreshAccounts();
        }

        public static void AddGrid()
        {
            s_addAccount.Background = s_light ? new SolidColorBrush(Colors.White) : new SolidColorBrush(Color.FromRgb(40, 45, 57));
            s_addAccount.HorizontalAlignment = HorizontalAlignment.Left;
            s_addAccount.VerticalAlignment = VerticalAlignment.Top;
            s_addAccount.Width = 140;
            s_addAccount.Height = 160;
            s_addAccount.MouseLeftButtonDown += Grid_MouseLeftButtonDown;

            var effect = new DropShadowEffect();
            effect.Color = s_light ? Colors.Gray : Colors.White;
            effect.ShadowDepth = 0;
            effect.BlurRadius = 7;
            effect.Opacity = 0.5;
            effect.Direction = 0;
            s_addAccount.Effect = effect;
            FontIcon icon = new FontIcon();
            icon.FontSize = 70;
            icon.HorizontalAlignment = HorizontalAlignment.Center;
            icon.VerticalAlignment = VerticalAlignment.Center;
            icon.Icon = SegoeFluentIcons.AddFriend;

            TextBlock text = new TextBlock();
            text.Text = "添加账号";
            text.TextWrapping = TextWrapping.Wrap;
            text.TextAlignment = TextAlignment.Center;
            text.Margin = new Thickness(0, 121, 0, 0);
            s_addAccount.Children.Add(icon);
            s_addAccount.Children.Add(text);
            s_stackPanel.Children.Add(s_addAccount);
            s_addAccount.Margin = new Thickness(5);
            s_addAccount.Style = s_triggerStyle;
        }
        

        public async static Task RefreshAccounts()
        {
            s_addedAccounts.Clear();
            s_grids.Clear();
            s_stackPanel.Children.Clear();
            s_stackPanel.Children.Add(s_addAccount);
            var accs = await AccountManager.GetAccounts(false);
            foreach (var acc in accs)
            {
                AddAccount(acc);
            }
        }

        public static void RefreshUi()
        {
            if (s_stackPanel == null) return;
            foreach (var grid in s_grids)
            {
                try
                {
                    if (!s_stackPanel.Children.Contains(grid))
                    {
                        s_stackPanel.Children.Add(grid);
                    }
                }
                catch { }
            }
        }

        public static void ChangeTheme(bool light)
        {
            s_light = light;

            foreach (var grid in s_grids)
            {
                grid.Background = s_light ? new SolidColorBrush(Colors.White) : new SolidColorBrush(Color.FromRgb(35, 39, 52));
                ((DropShadowEffect)grid.Effect).Color = s_light ? Colors.Gray : Colors.White;
            }
            s_addAccount.Background = s_light ? new SolidColorBrush(Colors.White) : new SolidColorBrush(Color.FromRgb(35, 39, 52));
            ((DropShadowEffect)s_addAccount.Effect).Color = s_light ? Colors.Gray : Colors.White;
        }

        public async static void AddAccount(Account.Account account)
        {
            if (s_addedAccounts.Contains(account.Id + account.Type.ToString())) return;
            s_addedAccounts.Add(account.Id + account.Type.ToString());
            Grid grid = new Grid();
            grid.Background = s_light ? new SolidColorBrush(Colors.White) : new SolidColorBrush(Color.FromRgb(40, 45, 57));
            grid.HorizontalAlignment = HorizontalAlignment.Left;
            grid.VerticalAlignment = VerticalAlignment.Top;
            grid.Width = 140;
            grid.Height = 160;
            var effect = new DropShadowEffect();
            effect.Color = s_light ? Colors.Gray : Colors.White;
            effect.ShadowDepth = 0;
            effect.BlurRadius = 7;
            effect.Opacity = 0.5;
            effect.Direction = 0;
            grid.Effect = effect;
            Image avator = new Image();
            avator.Source = ((Image) s_mainGrid.FindResource("AvatarImage")).Source;
            if (account.Type == AccountType.MSA)
            {
                Task.Run(async () => avator.Source = await AccountManager.GetAvatarAsync(account, 64));
            }
            avator.VerticalAlignment = VerticalAlignment.Top;
            avator.HorizontalAlignment = HorizontalAlignment.Center;
            avator.Width = 64;
            avator.Height = 64;
            Grid.SetRow(avator, 1);
            Grid.SetColumn(avator, 1);
            avator.Margin = new Thickness(0, 25, 0, 0);
            FontIcon icon = new FontIcon();
            icon.Margin = new Thickness(0, 136, 4, 4);
            icon.HorizontalAlignment = HorizontalAlignment.Right;
            TextBlock type = new TextBlock();
            type.Margin = new Thickness(28, 101, 28, 28);
            type.TextAlignment = TextAlignment.Center;
            grid.MouseLeftButtonDown += (s, e) => {
                MainWindow.Navigate(new AccountManagePage(account));
            };


            if(account.Type == AccountType.MSA)
            {
                type.Text = "微软账号";
                icon.Icon = SegoeFluentIcons.PassiveAuthentication;
            }
            else if(account.Type == AccountType.OFFLINE)
            {
                type.Text = "离线账号";
                icon.Icon = SegoeFluentIcons.NetworkOffline;
            }
            else if (account.Type == AccountType.AUTHLIB)
            {
                type.Text = "第三方账号";
                icon.Icon = SegoeFluentIcons.Library;
            }
            
            TextBlock name = new TextBlock();
            name.Text = account.Id;
            name.TextWrapping = TextWrapping.Wrap;
            name.TextAlignment = TextAlignment.Center;
            name.Margin = new Thickness(0, 121, 0, 0);
            grid.Children.Add(avator);
            grid.Children.Add(icon);
            grid.Children.Add(type);
            grid.Children.Add(name);
            grid.Margin = new Thickness(5);
            grid.Style = s_triggerStyle;
            s_grids.Add(grid);
            RefreshUi();
        }

        private static void Grid_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            MainWindow.AddAccPage();
        }

        private void Page_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            s_stackPanel.MaxWidth = this.ActualWidth - 50;
        }
    }
}
