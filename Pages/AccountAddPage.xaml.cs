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
            
            offl.Header = MainWindow.i18NTools.getString(offl.Header.ToString());
            ms.Header = MainWindow.i18NTools.getString(ms.Header.ToString());
            Msa.Content = MainWindow.i18NTools.getString(Msa.Content.ToString());
            cofn.Content = MainWindow.i18NTools.getString(cofn.Content.ToString());
            ofn.Content = MainWindow.i18NTools.getString(ofn.Content.ToString());
            if (MainWindow.i18NTools.getLangName().Equals("en_US"))
            {
                offl.FontFamily = new System.Windows.Media.FontFamily("Microsoft Yi Baiti");
                ms.FontFamily = new System.Windows.Media.FontFamily("Microsoft Yi Baiti");
                Msa.FontFamily = new System.Windows.Media.FontFamily("Microsoft Yi Baiti");
                cofn.FontFamily = new System.Windows.Media.FontFamily("Microsoft Yi Baiti");
                ofn.FontFamily = new System.Windows.Media.FontFamily("Microsoft Yi Baiti");
            }
            
        }

        private void back_Click(object sender, RoutedEventArgs e)
        {
            MainWindow.mnv.Navigate(typeof(AccountPage));
        }

        async private void Msa_Click(object sender, RoutedEventArgs e)
        {
            await OAuth.oa(Msa);
        }

        async private void Cofn_Click(object sender, RoutedEventArgs e)
        {
            string name = offlineName.Text;
            if(!string.IsNullOrEmpty(name))
            {
                LMC.Account.Account account = new LMC.Account.Account();
                account.id = name;
                account.type = 1;
                try
                {
                    await AccountManager.addAccount(account);
                    MainWindow.infobar.Severity = InfoBarSeverity.Success;
                    MainWindow.infobar.Message = MainWindow.i18NTools.getString("lmc.messages.offline.done");
                    MainWindow.infobar.IsClosable = true;
                    MainWindow.infobar.IsOpen = true;

                }
                catch (Exception ex)
                {
                    Logger logger = new Logger("AAP");
                    logger.warn("Some error occurred when adding offline account:" + ex.Message);
                }
            }
        }
    }
}
