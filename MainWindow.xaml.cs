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
                if (File.Exists($"./LMC/update.bat"))
                {
                    File.Delete($"./LMC/update.bat");
                    ShowDialog("确认", $"更新成功，可以关闭CMD命令行窗口，LMC已更新至{App.LauncherVersion}-{App.LauncherVersionType}，构建号{App.LauncherBuildVersion}", "提示");
                }
                Secrets.GetDeviceCode();
                var accounts = AccountManager.GetAccounts(false).Result;
                foreach ( var account in accounts)
                {
                    if(account.Type == AccountType.MSA)
                    {
                        AccountManager.GetAvatarAsync(account,64);
                    }
                }
            }
            catch {
                Environment.Exit(1);
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
