using System.Windows;
using LMC.Account;
using LMC.Minecraft;
using LMC.Minecraft.Launch;
using LMC.Minecraft.Profile;
using Page = iNKORE.UI.WPF.Modern.Controls.Page;

namespace LMC.Pages
{
    /// <summary>
    /// HomePage.xaml 的交互逻辑
    /// </summary>
    public partial class HomePage : Page
    {
        public HomePage()
        {
            InitializeComponent();
            Loaded += OnLoaded;
        }

        private async void OnLoaded(object sender, RoutedEventArgs e)
        {
            var p = await ProfileManager.GetSelectedProfile();
            ProfileButton.Content = "版本\n" + p.Name;
            var account = await AccountManager.GetSelectedAccount();
            string typeString = "";
            switch (account.Type)
            {
                case AccountType.MSA: typeString = "微软账号";break;
                case AccountType.AUTHLIB: typeString = "第三方账号";break;
                case AccountType.OFFLINE:
                {
                    if (account.Id.Equals("未添加账号")) break;
                    typeString = "离线账号";
                    break;
                }
            }
            AccountButton.Content = $"{typeString}\n{account.Id}";
        }

        private void ProfileButton_OnClick(object sender, RoutedEventArgs e)
        {
            MainWindow.Navigate(MainWindow.ProfilePage);
        }

        private void AccountButton_OnClick(object sender, RoutedEventArgs e)
        {
            MainWindow.Navigate(MainWindow.AccountPage);
        }

        private async void ButtonBase_OnClick(object sender, RoutedEventArgs e)
        {
            GameLaunchHelper gameLaunchHelper = new GameLaunchHelper();
            gameLaunchHelper.LaunchGame(await ProfileManager.GetSelectedProfile());
            MainWindow.Navigate(MainWindow.DownloadPage);
        }
    }
}
