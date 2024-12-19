using LMC.Account;
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
    /// <summary>
    /// HomePage.xaml 的交互逻辑
    /// </summary>
    public partial class AccountManagePage : Page
    {
        private static Account.Account s_account;
        public AccountManagePage(Account.Account acc)
        {
            InitializeComponent();
            s_account = acc;
            if(acc.Type == AccountType.MSA)
            {
                type.Text = "微软账号";
            }
            if (acc.Type == AccountType.OFFLINE)
            {
                type.Text = "离线账号";
            }
            if (acc.Type == AccountType.AUTHLIB)
            {
                type.Text = "第三方账号";
            }
            id.Text = acc.Id;
            this.Loaded += AccountManagePage_Loaded;
            this.Title = "账号管理 > " + acc.Id;
        }

        private async void AccountManagePage_Loaded(object sender, RoutedEventArgs e)
        { 
            if (s_account.Type == AccountType.MSA)
            {
                avat.Source = await AccountManager.GetAvatarAsync(s_account, 128);
            }
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            AccountManager.DeleteAccount(s_account);
            AccountPage.RefreshAccounts();
            MainWindow.MainFrame.Navigate(MainWindow.AccountPage);
        }

        private void Button_Click_1(object sender, RoutedEventArgs e)
        {
            MainWindow.Navigate(MainWindow.AccountPage);
        }
    }
}
