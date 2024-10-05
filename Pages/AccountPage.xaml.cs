﻿using iNKORE.UI.WPF.Controls;
using iNKORE.UI.WPF.Converters;
using iNKORE.UI.WPF.Modern;
using iNKORE.UI.WPF.Modern.Common.IconKeys;
using iNKORE.UI.WPF.Modern.Controls;
using LMC.Account;
using LMC.Basic;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Principal;
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
    public partial class AccountPage : Page
    {
        private static bool s_light = true;
        private static SimpleStackPanel s_stackPanel;
        private static List<Grid> s_grids = new List<Grid>();

        public AccountPage()
        {
            s_light = !string.IsNullOrEmpty(Config.ReadGlobal("ui", "theme")) ? Config.ReadGlobal("ui", "theme") == "light" : ThemeManager.Current.ApplicationTheme == ApplicationTheme.Light;
            InitializeComponent();
            s_stackPanel = ssp;

            Grid addAccount = new Grid();
            addAccount.Background = s_light ? new SolidColorBrush(Colors.White) : new SolidColorBrush(Color.FromRgb(40, 45, 57));
            addAccount.HorizontalAlignment = HorizontalAlignment.Left;
            addAccount.VerticalAlignment = VerticalAlignment.Top;
            addAccount.Width = 140;
            addAccount.Height = 160;
            addAccount.MouseLeftButtonDown += Grid_MouseLeftButtonDown;

            var effect = new DropShadowEffect();
            effect.Color = s_light ? Colors.Gray : Colors.White;
            effect.ShadowDepth = 0;
            effect.BlurRadius = 7;
            effect.Opacity = 0.5;
            effect.Direction = 0;
            addAccount.Effect = effect;
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
            addAccount.Children.Add(icon);
            addAccount.Children.Add(text);
            s_stackPanel.Children.Add(addAccount);
            s_grids.Add(addAccount);
        }

        public static void ChangeTheme(bool light)
        {
            s_light = light;
            foreach (var grid in s_grids)
            {
                grid.Background = s_light ? new SolidColorBrush(Colors.White) : new SolidColorBrush(Color.FromRgb(35, 39, 52));
            }
        }

        public static void AddAccount(Account.Account account)
        {
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
            BitmapImage source = new BitmapImage();
            source.BeginInit();
            source.UriSource = new Uri(@"E:\codes\line\LineLauncherCs\hutao.jpg");
            source.EndInit();
            avator.Source = source;
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
            s_stackPanel.Children.Add(grid);
            s_grids.Add(grid);
        }

        private void Grid_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            Account.Account account = new Account.Account();
            account.Id = "Fuck";
            account.Type = AccountType.OFFLINE;

            Account.Account account2 = new Account.Account();
            account2.Id = "Cnm";
            account2.Type = AccountType.MSA;

            Account.Account account3 = new Account.Account();
            account3.Id = "shit";
            account3.Type = AccountType.AUTHLIB;
            Random random = new Random();
            int i = random.Next(0, 3);
            if (i == 0)
            {
                AddAccount(account);
            }
            else if (i == 1)
            {
                AddAccount(account2);
            }
            else { 
                AddAccount(account3);
            }
        }
    }
}
