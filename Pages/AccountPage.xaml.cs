﻿using LMC.Account;
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
        private static List<Account.Account> s_accounts;
        private static ComboBox s_accountList;

        public AccountPage()
        {
            InitializeComponent();
            s_accountList = accountList;
            RefreshAccounts();
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

        public async static Task RefreshAccounts()
        {
            s_accountList.Items.Clear();
            s_accounts = await AccountManager.GetAccounts(false);
            foreach (var account in s_accounts)
            {
                string totalStr = account.Id;
                if(account.Type == AccountType.AUTHLIB)
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

        private void add_Click(object sender, RoutedEventArgs e)
        {
            MainWindow.MainNagView.Navigate(typeof(AccountAddPage));
        }

        async private void delete_Click(object sender, RoutedEventArgs e)
        {
            if(accountList.SelectedItem!= null) {
                await AccountManager.DeleteAccount(s_accounts.ElementAt(accountList.SelectedIndex));
            }
            await RefreshAccounts();
        }
    }
}
