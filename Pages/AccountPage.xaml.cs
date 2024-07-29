using LMC.Basic;
using LMC.Pages;
using LMC.Properties;
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

namespace LMC
{
    /// <summary>
    /// AccountPage.xaml 的交互逻辑
    /// </summary>
    public partial class AccountPage : Page
    {
        public AccountPage()
        {
            InitializeComponent();
            refreshContent();
        }
        public void refreshContent()
        {
            
            add.Content = MainWindow.i18NTools.getString(add.Content.ToString());
            delete.Content = MainWindow.i18NTools.getString(delete.Content.ToString());
            ait.Text = MainWindow.i18NTools.getString(ait.Text.ToString());
            if (MainWindow.i18NTools.getLangName().Equals("en_US"))
            {
                add.FontFamily = new System.Windows.Media.FontFamily("Microsoft Yi Baiti");
                delete.FontFamily = new System.Windows.Media.FontFamily("Microsoft Yi Baiti");
                ait.FontFamily = new System.Windows.Media.FontFamily("Microsoft Yi Baiti");
                accountinfo.FontFamily = new System.Windows.Media.FontFamily("Microsoft Yi Baiti");
                accountList.FontFamily = new System.Windows.Media.FontFamily("Microsoft Yi Baiti");
            }
        }

        private void add_Click(object sender, RoutedEventArgs e)
        {
            MainWindow.mnv.Navigate(typeof(AccountAddPage));
        }
    }
}
