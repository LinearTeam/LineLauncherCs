using System.Windows;

using iNKORE.UI.WPF.Modern.Controls;

namespace LMC
{
    /// <summary>
    /// MainWindow.xaml 的交互逻辑
    /// </summary>
    public partial class MainWindow : Window
    {
        public static string LauncherVersion = "2.0.0";

        public Pages.AboutPage AboutPage = new Pages.AboutPage();
        public Pages.AccountPage AccountPage = new Pages.AccountPage();
        public Pages.HomePage HomePage = new Pages.HomePage();
        public Pages.DownloadPage DownloadPage = new Pages.DownloadPage();
        public Pages.ProfilePage ProfilePage = new Pages.ProfilePage();
        public Pages.SettingPage SettingPage = new Pages.SettingPage();
        public Pages.LaunchPage LaunchPage = new Pages.LaunchPage();

        public MainWindow()
        {
            InitializeComponent();
        }

        private void NavigationView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
        {
            var item = sender.SelectedItem;
            Page? page = null;

            if (item == HomeNagVi)
            {
                page = HomePage;
            }
            else if (item == LaunchNagVi)
            {
                page = LaunchPage;
            }
            else if (item == ProfileNagVi)
            {
                page = ProfilePage;
            }
            else if (item == AccountNagVi)
            {
                page = AccountPage;
            }
            else if (item == AboutNagVi)
            {
                page = AboutPage;
            }
            else if (item == DownloadNagVi)
            {
                page = DownloadPage;
            }
            else if (item == SettingNagVi)
            {
                page = SettingPage;
            }

            if (page != null)
            {
                MainNagV.Header = page.Title;
                MainFrame.Navigate(page);
            }

        }
    }
}
