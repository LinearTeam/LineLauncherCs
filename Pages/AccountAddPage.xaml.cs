using LMC.Account;
using LMC.Account.OAuth;
using LMC.Basic;
using LMC.Properties;
using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Threading;
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
using Wpf.Ui.Controls;

namespace LMC.Pages
{
    /// <summary>
    /// AccountAddPage.xaml 的交互逻辑
    /// </summary>
    public partial class AccountAddPage : Page
    {
        public AccountAddPage()
        {
            InitializeComponent();
            refreshUiContent();
            string a = ms.FontFamily.ToString();
        }

        private void refreshUiContent()
        {
            try
            {
                offl.Header = MainWindow.i18NTools.getString(offl.Header.ToString());
                ms.Header = MainWindow.i18NTools.getString(ms.Header.ToString());
                Msa.Content = MainWindow.i18NTools.getString(Msa.Content.ToString());
                if (MainWindow.i18NTools.getLangName().Equals("en_US"))
                {
                    offl.FontFamily = new System.Windows.Media.FontFamily("Microsoft Yi Baiti");
                    ms.FontFamily = new System.Windows.Media.FontFamily("Microsoft Yi Baiti");
                    Msa.FontFamily = new System.Windows.Media.FontFamily("Microsoft Yi Baiti");
                }
            }
            catch (Exception e)
            {
                //TODO: tell user
                return;
            }

        }

        private void back_Click(object sender, RoutedEventArgs e)
        {
            MainWindow.mnv.Navigate(typeof(AccountPage));
        }

        async private void Msa_Click(object sender, RoutedEventArgs e)
        {
            await OAuth.oa();
        }
    }
}
