using System.Windows;
using LMC.Minecraft;
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
        }

        private void ProfileButton_OnClick(object sender, RoutedEventArgs e)
        {
            MainWindow.Navigate(MainWindow.ProfilePage);
        }
    }
}
