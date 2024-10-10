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
using System.Windows.Threading;
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
                await MainWindow.ShowDialog("确认","似乎发生了意想不到的情况，即在开始登录时已经有一个登录请求正在进行，请反馈此问题。", "提示", ContentDialogButton.Close);
                return;
            }

            s_contentDialog = new ContentDialog();
            s_contentDialog.Title = "微软登录";
            var content = new SimpleStackPanel();
            content.Orientation = Orientation.Vertical;
            var text = new TextBlock();
            text.FontSize = 12;
            text.Text = "正在等待用户操作...";
            text.TextWrapping = TextWrapping.Wrap;
            content.Spacing = 30;
            content.Children.Add(text);
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
                    text.Text = "登录成功！";
                    progressBar.IsIndeterminate = false;
                    s_contentDialog.CloseButtonText = "确认";
                    var button = new Button();
                    return;
                }
                if (res.done == 1)
                {
                    progressBar.ShowError = true;
                    text.Text = "登录失败！请检查网络连接并重试，若仍无法登录请反馈此Bug。";
                    s_contentDialog.CloseButtonText = "确认";
                    return;
                }
                if(res.done == 2)
                {
                    progressBar.ShowError = true;
                    text.Text = "登录失败！可能是由于该账号没有购买Minecraft导致的，若购买，请前往官网进行一次登录并设置档案名后重试。";
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
                if(res.done == 3)
                {

                    progressBar.ShowError = true;
                    text.Text = "登录失败！回调地址被以不正确的方式访问，导致无法识别授权码参数，请重试。除非特别确定问题与启动器有关，否则请勿反馈此问题。";
                    s_contentDialog.CloseButtonText = "确认";
                    return;
                }
            }, () => {
                text.Text = "已获取到授权码，正在登录...";
            });
        }

        bool cancel = false;

        private async void Button_Click_2(object sender, RoutedEventArgs e)
        {
            cancel = false;
            if (!OAuth.CanOA())
            {
                await MainWindow.ShowDialog("确认", "似乎发生了意想不到的情况，即在点击开始登录时已经有一个登录请求正在进行，请反馈此问题。", "提示", ContentDialogButton.Close);
                return;
            }

            s_contentDialog = new ContentDialog();
            s_contentDialog.Title = "微软登录";
            var content = new SimpleStackPanel();
            content.Orientation = Orientation.Vertical;
            var text = new TextBlock();
            text.FontSize = 12;
            text.Text = "正在获取授权码...";
            text.TextWrapping = TextWrapping.Wrap;
            content.Spacing = 30;
            content.Children.Add(text);
            var progressBar = new ProgressBar();
            progressBar.IsIndeterminate = true;
            content.Children.Add(progressBar);
            s_contentDialog.Content = content;
            s_contentDialog.CloseButtonText = "取消";
            s_contentDialog.ShowAsync();

            OAuth oa = new OAuth();
            var res = await oa.DeviceCodeOA();
            text.Text = res.msg + "代码已自动复制。";
            var button = new Button();
            var stackpanel = new SimpleStackPanel();
            stackpanel.Spacing = 10;
            stackpanel.Orientation = Orientation.Horizontal;
            stackpanel.HorizontalAlignment = HorizontalAlignment.Right;
            button.Content = "打开网页";
            button.Click += (a, b) => {
                System.Diagnostics.Process.Start("explorer.exe", "\"https://www.microsoft.com/link\"");
            };
            stackpanel.Children.Add(button);
            var copyButton = new Button();
            copyButton.Content = "复制代码";
            copyButton.Click += (a, b) =>
            {
                Clipboard.SetText(res.usercode);
            };
            stackpanel.Children.Add(copyButton);
            content.Children.Add(stackpanel);
            Clipboard.SetText(res.usercode);
            DispatcherTimer timer = new DispatcherTimer();
            Dictionary<string, string> result = new Dictionary<string, string>();
            timer.Interval = TimeSpan.FromSeconds(res.interval);
            timer.Tick += async (a, b) => {
                if (cancel) return;
                var r = await oa.CheckResult(res.devicecode);
                if (r.result == 1)
                {
                    return;
                }
                if (r.result == 2)
                {
                    content.Children.Remove(stackpanel);
                    progressBar.ShowError = true;
                    text.Text = "登录失败，设备码已过期，请重新登录。";
                    return;
                }
                if (r.result == 3)
                {
                    content.Children.Remove(stackpanel);
                    progressBar.ShowError = true;
                    text.Text = "登录失败，用户在浏览器中拒绝授权，请重新登录并允许LMC访问用户信息。";
                    return;
                }
                result.Add("a", r.accessToken);
                result.Add("r", r.refreshToken);
                timer.Stop();
                timer.IsEnabled = false;
            };
            timer.Start();


            while (true)
            {
                if(cancel) return;
                await Task.Delay(1000);
                if (timer.IsEnabled == false)
                {
                    string atoken = result["a"];
                    string rtoken = result["r"];
                    var r = await oa.StartOA(atoken);
                    if (r.done == 0)
                    {
                        AccountManager.AddAccount(r.account, rtoken);
                        text.Text = "登录成功！";
                        progressBar.IsIndeterminate = false;
                        s_contentDialog.CloseButtonText = "确认";
                        content.Children.Remove(stackpanel);
                        return;
                    }
                    if (r.done == 1)
                    {
                        progressBar.ShowError = true;
                        text.Text = "登录失败！请检查网络连接并重试，若仍无法登录请反馈此Bug。";
                        s_contentDialog.CloseButtonText = "确认";
                        content.Children.Remove(stackpanel);
                        return;
                    }
                    if (r.done == 2)
                    {
                        progressBar.ShowError = true;
                        text.Text = "登录失败！可能是由于该账号没有购买Minecraft导致的，若购买，请前往官网进行一次登录并设置档案名后重试。";
                        s_contentDialog.CloseButtonText = "确认";
                        button.HorizontalAlignment = HorizontalAlignment.Right;
                        button.Content = "打开MC官网";
                        button.Click += (a, b) => {

                            System.Diagnostics.Process.Start("explorer.exe", "\"https://www.minecraft.net/profile\"");
                        };
                        stackpanel.Children.Remove(copyButton);
                        return;
                    }
                    break;
                }
            }
        }
    }
}
