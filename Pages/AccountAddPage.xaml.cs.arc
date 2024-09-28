using LMC.Account;
using LMC.Account.OAuth;
using LMC.Basic;
using LMC.Properties;
using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
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

        private static bool s_isNameAvail = false; 
        public AccountAddPage()
        {
            InitializeComponent();
            TextBox_TextChanged(null, null);
        }

        

        private void back_Click(object sender, RoutedEventArgs e)
        {
            MainWindow.MainNagView.Navigate(typeof(AccountPage));
        }

        private void Slider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            try
            {
                ltc.SelectedIndex = (int)lsl.Value - 1;
            }
            catch
            {
                return;
            }
        }

        async private void msa_Click(object sender, RoutedEventArgs e)
        {
            await OAuth.OA();
        }

        async private void Button_Click(object sender, RoutedEventArgs e)
        {
            if (!s_isNameAvail) return;
            lb.IsEnabled = false;
            string name = ltb.Text;
            if (!string.IsNullOrEmpty(name))
            {
                LMC.Account.Account account = new LMC.Account.Account();
                account.Id = name;
                account.Type = AccountType.OFFLINE;
                try
                {
                    await AccountManager.AddAccount(account);
                    MainWindow.InfoBar.Severity = InfoBarSeverity.Success;
                    MainWindow.InfoBar.Message = "添加成功！";
                    MainWindow.InfoBar.IsClosable = true;
                    await Task.Delay(700);
                    MainWindow.InfoBar.IsOpen = true;

                }
                catch (Exception ex)
                {
                    Logger logger = new Logger("AAP");
                    logger.Warn("Some error occurred when adding offline account:" + ex.Message);
                    MainWindow.InfoBar.Severity = InfoBarSeverity.Error;
                    MainWindow.InfoBar.Message = "添加失败，请查看日志并反馈此问题！";
                    MainWindow.InfoBar.IsClosable = true;
                    await Task.Delay(700);
                    MainWindow.InfoBar.IsOpen = true;
                }
            }
            lb.IsEnabled = true;
        }
        private void TextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (string.IsNullOrEmpty(ltb.Text))
            {
                stb.Foreground = new SolidColorBrush(Color.FromScRgb(100, 2, 1, 0));
                stb.Content = "请输入ID！";
                s_isNameAvail = false;
                return;
            }
            if(!Regex.IsMatch(ltb.Text, @"^[a-zA-Z0-9_]+$"))
            {
                stb.Foreground = new SolidColorBrush(Color.FromScRgb(100, 1, 0, 0));
                stb.Content= "请勿在ID中包含除了字母、数字、下划线以外的内容！";
                s_isNameAvail = false;
                return;
            }
            stb.Foreground = new SolidColorBrush(Color.FromScRgb(127, 0, 1, 0));
            stb.Content= "ID可用！";
            s_isNameAvail = true;
            return;
        }
    }
}