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
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Page = iNKORE.UI.WPF.Modern.Controls.Page;

namespace LMC.Pages
{
    public class ProfileItem
    {
        public ImageSource Image { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
    }
    
    /// <summary>
    /// HomePage.xaml 的交互逻辑
    /// </summary>
    public partial class ProfilePage : Page
    {
        
        public ProfilePage()
        {
            InitializeComponent();
        }

        private void ButtonBase_OnClick(object sender, RoutedEventArgs e)
        {
            MainWindow.Navigate(DownloadPage.DownloadMinecraftPage);
        }
    }
}
