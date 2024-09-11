using LMC.Account;
using LMC.Basic;
using LMC.Pages;
using LMC.Properties;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
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
using System.Windows.Threading;

namespace LMC
{
    /// <summary>
    /// AccountPage.xaml 的交互逻辑
    /// </summary>
    public partial class AccountPage : Page
    {
        private static List<Account.Account> s_accounts;
        private static ComboBox s_accountList;
        private static Logger s_logger = new Logger("AP");
        private DispatcherTimer _timer;

        public AccountPage()
        {
            InitializeComponent();
            s_accountList = accountList;
            this.Loaded += PageLoaded;
            /*            refreshContent();
                    }
                    public void refreshContent()
                    {

                        add.Content = MainWindow.I18NTools.getString(add.Content.ToString());
                        delete.Content = MainWindow.I18NTools.getString(delete.Content.ToString());
                        ait.Text = MainWindow.I18NTools.getString(ait.Text.ToString());
                        if (MainWindow.I18NTools.getLangName().Equals("en_US"))
                        {
                            add.FontFamily = new System.Windows.Media.FontFamily("Microsoft Yi Baiti");
                            delete.FontFamily = new System.Windows.Media.FontFamily("Microsoft Yi Baiti");
                            ait.FontFamily = new System.Windows.Media.FontFamily("Microsoft Yi Baiti");
                            accountinfo.FontFamily = new System.Windows.Media.FontFamily("Microsoft Yi Baiti");
                            accountList.FontFamily = new System.Windows.Media.FontFamily("Microsoft Yi Baiti");
                        }
            */
        }

        private async void PageLoaded(object sender, RoutedEventArgs e)
        {
            await RefreshAccounts(false);
        }

        public async static Task RefreshAccounts(bool refresh)
        {
            try
            {
                s_logger.Info("Refresh accounts : " + refresh.ToString());
                s_accounts = await AccountManager.GetAccounts(refresh);
                s_accountList.Items.Clear();
                foreach (var account in s_accounts)
                {
                    string totalStr = account.Id;
                    if (account.Type == AccountType.AUTHLIB)
                    {
                        totalStr += " - 第三方";
                    }
                    if (account.Type == AccountType.MSA)
                    {
                        totalStr += " - 微软";
                    }
                    if (account.Type == AccountType.OFFLINE)
                    {
                        totalStr += " - 离线";
                    }
                    s_accountList.Items.Add(totalStr);
                }
            }
            catch(CryptographicException ex)
            {
                s_logger.Warn("Failed to refresh account: " + ex.Message + "\n" + ex.StackTrace);
                Secrets.Backup("Can't refresh account");
                await MainWindow.ShowMsgBox("提示", "LMC的全局设置（如账号、游戏文件夹列表）等可能由于重装系统、更换硬件等原因导致无法解密，已留下一份备份，请更换回原来的硬件并在 设置 -> 安全设置 -> 导出全局设置 中导出设置，然后在 设置 -> 安全设置 -> 导入全局设置 中导入。若无法找回原来的硬件或是重装系统导致的解密失败，则无法恢复。\n\n注：为啥重装系统会改BIOS和CPU信息？？？ ", "确定");
            }
        }

        private void add_Click(object sender, RoutedEventArgs e)
        {
            MainWindow.MainNagView.Navigate(typeof(AccountAddPage));
        }

        async private void delete_Click(object sender, RoutedEventArgs e)
        {
            if(accountList.SelectedItem!= null) {
                AccountManager.DeleteAccount(s_accounts.ElementAt(accountList.SelectedIndex));
            }
            await RefreshAccounts(false);
        }

        public static Account.Account GetSelectedAccount()
        {
            try
            {
                return s_accounts.ElementAt(s_accountList.SelectedIndex);
            }
            catch {
                return null;
            }
        }
    }
}
