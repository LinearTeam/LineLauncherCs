using iNKORE.UI.WPF.Controls;
using iNKORE.UI.WPF.Modern;
using iNKORE.UI.WPF.Modern.Controls;
using LMC.Account;
using LMC.Account.OAuth;
using LMC.Basic;
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
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Page = iNKORE.UI.WPF.Modern.Controls.Page;
using ProgressBar = iNKORE.UI.WPF.Modern.Controls.ProgressBar;

namespace LMC.Pages.AccountTypes
{
    /// <summary>
    /// HomePage.xaml 的交互逻辑
    /// </summary>
    public partial class MicrosoftLoginPage : Page
    {
        public MicrosoftLoginPage()
        {
            InitializeComponent();
        }

        private static ContentDialog s_contentDialog = new ContentDialog();
        
        private void Button_Click(object sender, RoutedEventArgs e)
        {
            MainWindow.AddAccPage();
        }

        private async void Button_Click_1(object sender, RoutedEventArgs e)
        {
            if (!OAuth.CanOA())
            {
                await MainWindow.ShowDialog("确认","似乎发生了意想不到的情况，即在点击 “打开网页” 按钮时已经有一个登录请求正在进行，请反馈此问题。", "提示", ContentDialogButton.Close);
                return;
            }
            s_contentDialog = new ContentDialog();
            s_contentDialog.Title = "微软登录";
            var content = new SimpleStackPanel();
            content.Orientation = Orientation.Vertical;
            var label = new Label();
            label.FontSize = 12;
            label.Content = "正在等待用户操作...";
            content.Spacing = 30;
            content.Children.Add(label);
            var progressBar = new ProgressBar();
            progressBar.IsIndeterminate = true;
            content.Children.Add(progressBar);
            s_contentDialog.Content = content;
            s_contentDialog.CloseButtonText = "取消";
            s_contentDialog.Closed += (a, b) =>
            {
                OAuth.CancelOA();
            };
            s_contentDialog.ShowAsync();
            await OAuth.OA(async (res) =>
            {
                if(res.done == 0)
                {
                    AccountManager.AddAccount(res.account, res.refreshToken);
                    label.Content = "登录成功！";
                    progressBar.IsIndeterminate = false;
                    s_contentDialog.CloseButtonText = "确认";
                    var button = new Button();
                    return;
                }
                if (res.done == 1)
                {
                    progressBar.ShowError = true;
                    label.Content = "登录失败！请检查网络连接并重试，若仍无法登录请反馈此Bug。";
                    s_contentDialog.CloseButtonText = "确认";
                    return;
                }
                if(res.done == 2)
                {
                    progressBar.ShowError = true;
                    label.Content = "登录失败！可能是由于该账号没有购买Minecraft导致的，若购买，请前往官网进行一次登录并设置档案名后重试。";
                    s_contentDialog.CloseButtonText = "确认";
                    var button = new Button();
                    button.HorizontalAlignment = HorizontalAlignment.Right;
                    button.Content = "打开MC官网";
                    button.Click += (a, b) => {

                        System.Diagnostics.Process.Start("explorer.exe", "\"https://www.minecraft.net/profile\"");
                    };
                    content.Children.Add(button);
                    return;
                }
            }, () => {
                label.Content = "已获取到授权码，正在登录...";
            });
        }
    }
}
