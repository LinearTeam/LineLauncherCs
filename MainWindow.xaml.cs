using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using Common.Models;
using iNKORE.UI.WPF.Modern.Controls;
using LMC.Basic;
using Page = iNKORE.UI.WPF.Modern.Controls.Page;
using MessageBox = iNKORE.UI.WPF.Modern.Controls.MessageBox;
using Frame = iNKORE.UI.WPF.Modern.Controls.Frame;
using LMC.Account;
using LMC.Pages;
using LMC.Utils;
using System.Reflection.Emit;

namespace LMC
{
    /// <summary>
    /// MainWindow.xaml 的交互逻辑
    /// </summary>
    public partial class MainWindow : Window
    {
        private static Logger s_logger = new Logger("MainUI");
        private static Image s_background;
        public static Pages.AboutPage AboutPage = new Pages.AboutPage();
        public static Pages.AccountPage AccountPage = new Pages.AccountPage();
        public static Pages.HomePage HomePage = new Pages.HomePage();
        public static Pages.DownloadPage DownloadPage = new Pages.DownloadPage();
        public static Pages.ProfilePage ProfilePage = new Pages.ProfilePage();
        public static Pages.SettingPage SettingPage = new Pages.SettingPage();
        public static Pages.LaunchPage LaunchPage = new Pages.LaunchPage();
        public static Pages.AddAccountPage AddAccountPage = new Pages.AddAccountPage();

        public static Frame MainFrame;

        public MainWindow()
        {
            try
            {
                LineFileParser lfp = new LineFileParser();
                s_logger.Info("正在初始化主界面");
                AccountManager.GetAccounts(false);
                AccountManager.GetAccounts(true);
                InitializeComponent();
                s_background = BackGround;
                MainFrame = MainFrm;
                double width;
                if(double.TryParse(Config.ReadGlobal("window", "width"), out width))
                {
                    this.Width = width;
                }
                double height;
                if (double.TryParse(Config.ReadGlobal("window", "height"), out height))
                {
                    this.Height = height;
                }
                if (File.Exists($"./LMC/update.bat"))
                {
                    File.Delete($"./LMC/update.bat");
                    ShowDialog("确认", $"更新成功，可以关闭CMD命令行窗口，LMC已更新至{App.LauncherVersion}-{App.LauncherVersionType}，构建号{App.LauncherBuildVersion}，更新内容：\n{App.LauncherUpdateLog}", "提示");
                }
                Secrets.GetDeviceCode();
                var accounts = AccountManager.GetAccounts(false).Result;
                foreach (var account in accounts)
                {
                    if (account.Type == AccountType.MSA)
                    {
                        AccountManager.GetAvatarAsync(account, 64);
                    }
                }
                this.Loaded += MainWindow_Loaded;
                this.SizeChanged += MainWindow_SizeChanged;
            }
            catch {
                Environment.Exit(1);
            }
        }

        private void MainWindow_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            double width = this.ActualWidth;
            double height = this.ActualHeight;
            Config.WriteGlobal("window", "width", width.ToString());
            Config.WriteGlobal("window", "height", height.ToString());
        }

        private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            s_logger.Info("正在检查更新");
            try
            {
                var ver = await UpdateChecker.Check();

                if (ver == null)
                {
                    throw new Exception("检查更新失败，版本为空");
                }
                if (ver.Type == App.LauncherVersionType && ver.Version == App.LauncherVersion && ver.Build == App.LauncherBuildVersion) {
                    s_logger.Info("当前是最新版");
                    return;
                }
                if (!ver.SecurityOrEmergency)
                {
                    var res = await ShowDialog("取消", $"发现新的非紧急更新版！\n版本号：{ver.Version}\n类型：{ver.Type}\n构建号：{ver.Build}", "更新", ContentDialogButton.Primary, "更新");
                    if (res == ContentDialogResult.Primary)
                    {
                        try
                        {
                            await UpdateChecker.Update(ver);
                        }
                        catch (Exception ex)
                        {
                            new Logger("MW-UPD").Error($"更新失败：{ex.Message}\n{ex.StackTrace}");
                        }
                    }
                }
                else
                {
                    ContentDialog dialog = new ContentDialog();
                    dialog.Title = "更新";
                    dialog.Content = $"发现新的紧急更新版！\n版本号：{ver.Version}\n类型：{ver.Type}\n构建号：{ver.Build}";
                    dialog.PrimaryButtonText = "更新";
                    dialog.DefaultButton = ContentDialogButton.Primary;
                    dialog.PrimaryButtonClick += async (s, e) =>
                    {
                        try
                        {
                            await UpdateChecker.Update(ver);
                        }
                        catch (Exception ex)
                        {
                            new Logger("AP-UPD").Error($"更新失败：{ex.Message}\n{ex.StackTrace}");
                        }
                    };
                    dialog.ShowAsync();

                }
            }
            catch (Exception ex)
            {
                s_logger.Error("更新检查失败：" + ex.Message + "\n" + ex.StackTrace);
                ShowDialog("确认", $"更新检查失败：{ex.Message}\n{ex.StackTrace}", "错误");
            }

        }
    

        public static void AddAccPage()
        {
            MainFrame.Navigate(AddAccountPage);
        }

        public async static Task<ContentDialogResult> ShowDialog(string closeButtonText, string content, string title, ContentDialogButton defaultButton = ContentDialogButton.None, string primaryButtonText = null, string secondaryButtonText = null)
        {
            short temp = (short) new Random().Next(1000, 9999);
            s_logger.Info($"正在显示Dialog{temp}：\nContent: {content}\nTitle: {title}\nSecButton: {secondaryButtonText}\nPrimButton: {primaryButtonText}\nDef: {defaultButton}\nClose: {closeButtonText}");
            ContentDialog dialog = new ContentDialog();
            dialog.Content = content;
            dialog.Title = title;
            if (primaryButtonText != null)
            {
                dialog.PrimaryButtonText = primaryButtonText;
            }
            if (secondaryButtonText == null)
            {
                dialog.SecondaryButtonText = closeButtonText;
            }
            else
            {
                dialog.CloseButtonText = closeButtonText;
            }
            dialog.DefaultButton = defaultButton == ContentDialogButton.Close && secondaryButtonText == null ? ContentDialogButton.Secondary : defaultButton;
            var res = await dialog.ShowAsync();
            res = res == ContentDialogResult.Secondary && secondaryButtonText == null ? ContentDialogResult.None : res;
            s_logger.Info($"Dialog{temp}的用户操作为{res}");
            return res;
        }

        private void NavigationView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
        {
            Page? page = null;
            var item = args.SelectedItem;
            if (item == HomeNagVi)
            {
                page = HomePage;
            }
            else if (item == LaunchNagVi)
            {
                page = LaunchPage;
            }
            else if (item == ProfileNagVi)
            {
                page = ProfilePage;
            }
            else if (item == AccountNagVi)
            {
                page = AccountPage;
            }
            else if (item == AboutNagVi)
            {
                page = AboutPage;
            }
            else if (item == DownloadNagVi)
            {
                page = DownloadPage;
            }
            else if (item == SettingNagVi)
            {
                page = SettingPage;
            }

            if (page != null)
            {
                MainNagV.Header = page.Title;
                MainFrame.Navigate(page);
            }
        }

        public static void ShowMsgBox(string title, string content, Action action = null)
        {
            s_logger.Info($"正在显示MsgBox：" +
                $"\nTitle: {title}" +
                $"\nContent: {content}" +
                $"\nAction: {action.GetType()}");
            var msgButton = MessageBoxButton.OK;
            var res = MessageBox.Show(content,title, msgButton);
            if (action != null)
            {
                action.Invoke();
            }
        }
    }
}
