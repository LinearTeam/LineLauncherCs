using System.Windows;
using iNKORE.UI.WPF.Modern.Controls;
using LMC.Account;
using Page = iNKORE.UI.WPF.Modern.Controls.Page;

namespace LMC.Pages
{
    /// <summary>
    /// HomePage.xaml 的交互逻辑
    /// </summary>
    public partial class AccountManagePage : Page
    {
        private Account.Account _account;
        public AccountManagePage(Account.Account acc)
        {
            InitializeComponent();
            _account = acc;
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
            if (_account.Type == AccountType.MSA)
            {
                avat.Source = await AccountManager.GetAvatarAsync(_account, 128);
            }
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            AccountManager.DeleteAccount(_account);
            AccountPage.RefreshAccounts();
            MainWindow.MainFrame.Navigate(MainWindow.AccountPage);
        }

        private void Button_Click_1(object sender, RoutedEventArgs e)
        {
            MainWindow.Navigate(MainWindow.AccountPage);
        }

        private async void Select(object sender, RoutedEventArgs e)
        {
            await AccountManager.SetSelectedAccount(_account);
            MainWindow.Instance.EnqueueMessage(new InfoBarMessage("",InfoBarSeverity.Success,"已选择"));
        }
    }
}
