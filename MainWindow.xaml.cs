using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Timers;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using iNKORE.UI.WPF.Controls;
using iNKORE.UI.WPF.Modern;
using iNKORE.UI.WPF.Modern.Controls;
using LMC.Account;
using LMC.Basic;
using LMC.Pages;
using LMC.Utils;
using Page = iNKORE.UI.WPF.Modern.Controls.Page;
using MessageBox = iNKORE.UI.WPF.Modern.Controls.MessageBox;
using Frame = iNKORE.UI.WPF.Modern.Controls.Frame;

namespace LMC
{
    /// <summary>
    ///     MainWindow.xaml 的交互逻辑
    /// </summary>
    public partial class MainWindow : Window
    {
        private static readonly Logger s_logger = new Logger("MainUI");
        private static Image s_background;
        private static InfoBadge s_taskInfoBadge;
        public static AboutPage AboutPage = new AboutPage();
        public static AccountPage AccountPage = new AccountPage();
        public static HomePage HomePage = new HomePage();
        public static TaskPage DownloadPage = new TaskPage();
        public static ProfilePage ProfilePage = new ProfilePage();
        public static SettingPage SettingPage = new SettingPage();
        public static DownloadPage LaunchPage = new DownloadPage();
        public static AddAccountPage AddAccountPage = new AddAccountPage();
        public static MainWindow Instance;
        
        private static readonly Stack<NavigationViewItem> s_lastSelectedItem = new Stack<NavigationViewItem>();

        private static readonly Stack<Page> s_accessPages = new Stack<Page>();

        public static Frame MainFrame;
        public static NavigationView MainNagView;


        private readonly ConcurrentQueue<InfoBarMessage> _messageQueue = new ConcurrentQueue<InfoBarMessage>();
        private readonly object _syncRoot = new object();
        private InfoBar[] _infoBars;

        public MainWindow()
        {
            Instance = this;
            try
            {
                var lfp = new LineFileParser();
                s_logger.Info("正在初始化主界面");
                AccountManager.GetAccounts(false);
                AccountManager.GetAccounts(true);
                InitializeComponent();
                bool light = !string.IsNullOrEmpty(Config.ReadGlobal("ui", "theme"))
                    ? Config.ReadGlobal("ui", "theme") == "light"
                    : ThemeManager.Current.ApplicationTheme == ApplicationTheme.Light;
                ThemeManager.Current.ApplicationTheme = light ? ApplicationTheme.Light : ApplicationTheme.Dark;
                SettingPage.Theme.IsOn = light;

                s_background = BackGround;
                MainFrame = MainFrm;
                MainNagView = MainNagV;
                s_taskInfoBadge = taskIb;
                if (Config.ReadGlobal("window", "maximized") == "1")
                {
                    WindowState = WindowState.Maximized;
                }
                else
                {
                    double width;
                    if (double.TryParse(Config.ReadGlobal("window", "width"), out width))
                    {
                        Width = width;
                    }

                    double height;
                    if (double.TryParse(Config.ReadGlobal("window", "height"), out height))
                    {
                        Height = height;
                    }
                }

                Secrets.GetDeviceCode();
                var accounts = AccountManager.GetAccounts(false).Result;
                foreach (var account in accounts)
                    if (account.Type == AccountType.MSA)
                    {
                        AccountManager.GetAvatarAsync(account, 64);
                    }

                Loaded += MainWindow_Loaded;
                SizeChanged += MainWindow_SizeChanged;
                StateChanged += MainWindow_StateChanged;
                KeyDown += MainWindow_KeyDown;
                InfoBar[] arr ={ibf, ibs, ibt};
                InfoBarMessageService(arr);
            }
            catch
            {
                Environment.Exit(1);
            }
        }

        private async void MainWindow_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                if (s_accessPages.Count != 0)
                {
                    Navigate(s_accessPages.Pop(), false);
                }

                e.Handled = true;
            }
        }

        private void MainWindow_StateChanged(object sender, EventArgs e)
        {
            Config.WriteGlobal("window", "maximized", WindowState == WindowState.Maximized ? "1" : "0");
        }

        private void MainWindow_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            double width = ActualWidth;
            double height = ActualHeight;
            Config.WriteGlobal("window", "width", width.ToString());
            Config.WriteGlobal("window", "height", height.ToString());
        }


        private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            s_logger.Info("正在检查更新");
            try
            {
                var ver = await Updater.Check();

                if (ver == null)
                {
                    throw new Exception("检查更新失败，版本为空");
                }

                if (ver.Type == App.LauncherVersionType && ver.Version == App.LauncherVersion && ver.Build == App.LauncherBuildVersion)
                {
                    s_logger.Info("当前是最新版");

                    if (File.Exists("./LMC/update.bat"))
                    {
                        File.Delete("./LMC/update.bat");
                        var res = await ShowDialog("确认",
                            $"更新成功，可以关闭CMD命令行窗口，LMC已更新至{App.LauncherVersion}-{App.LauncherVersionType}，构建号{App.LauncherBuildVersion}，更新内容：\n{App.LauncherUpdateLog}", "提示",
                            ContentDialogButton.Close, "打开GitHub");
                        if (res == ContentDialogResult.Primary)
                        {
                            Process.Start("explorer", "\"https://www.github.com/LinearTeam/LineLauncherCs/releases/latest\"");
                        }
                    }
                }
                else
                {
                    if (!ver.SecurityOrEmergency)
                    {
                        var res = await ShowDialog("取消", $"发现新的非紧急更新版！\n版本号：{ver.Version}\n类型：{ver.Type}\n构建号：{ver.Build}", "更新", ContentDialogButton.Primary, "更新");
                        if (res == ContentDialogResult.Primary)
                        {
                            try
                            {
                                await Updater.Update(ver);
                            }
                            catch (Exception ex)
                            {
                                new Logger("MW-UPD").Error($"更新失败：{ex.Message}\n{ex.StackTrace}");
                            }
                        }
                    }
                    else
                    {
                        var dialog = new ContentDialog();
                        dialog.Title = "更新";
                        dialog.Content = $"发现新的紧急更新版！\n版本号：{ver.Version}\n类型：{ver.Type}\n构建号：{ver.Build}";
                        dialog.PrimaryButtonText = "更新";
                        dialog.DefaultButton = ContentDialogButton.Primary;
                        dialog.PrimaryButtonClick += async (s, e) =>
                        {
                            try
                            {
                                await Updater.Update(ver);
                            }
                            catch (Exception ex)
                            {
                                new Logger("AP-UPD").Error($"更新失败：{ex.Message}\n{ex.StackTrace}");
                            }
                        };
                        await dialog.ShowAsync();

                    }
                }
            }
            catch (Exception ex)
            {
                s_logger.Error("更新检查失败：" + ex.Message + "\n" + ex.StackTrace);
                await ShowDialog("确认", $"更新检查失败：{ex.Message}\n{ex.StackTrace}", "错误");
            }
            
            try
            {
                s_logger.Info("正在检查隐私政策");
                string noticeFile = await HttpUtils.GetString("https://huangyu.win/LMC/privacy.line");
                s_logger.Info("获取到的隐私政策文件: \n" + noticeFile);
                File.WriteAllText("./LMC/temp/privacy.line", noticeFile);
                var lfp = new LineFileParser();
                string id = lfp.Read("./LMC/temp/privacy.line", "privacyId", "privacy");
                if (Config.ReadGlobal("privacy", "id") != id)
                {
                    s_logger.Info("隐私政策未被查看，V: " + Config.ReadGlobal("privacy", "id") + "/" + id);
                    string content = lfp.Read("./LMC/temp/privacy.line", "link", "privacy");
                    ContentDialog cd = new ContentDialog();
                    cd.Title = "隐私政策";
                    SimpleStackPanel ssp = new SimpleStackPanel();
                    ssp.Orientation = Orientation.Vertical;
                    Label l = new Label();
                    l.Content = $"在使用LMC前，请先阅读我们的隐私政策：\n{content}";
                    ssp.Children.Add(l);
                    Button b = new Button();
                    b.Content = "查看隐私政策";
                    b.Click += async (s, e) => System.Diagnostics.Process.Start("explorer.exe", $"\"{content}\"");
                    b.VerticalAlignment = VerticalAlignment.Bottom;
                    b.HorizontalAlignment = HorizontalAlignment.Right;
                    ssp.Children.Add(b);
                    ssp.Spacing = 15;
                    cd.Content = ssp;
                    cd.PrimaryButtonText = "同意";
                    cd.SecondaryButtonText = "拒绝";
                    cd.PrimaryButtonClick += (dialog, args) =>
                    {
                        Config.WriteGlobal("privacy", "id", id);
                        Secrets.Write("pt", "t", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                        s_logger.Info("用户同意了隐私政策");
                    };
                    cd.SecondaryButtonClick += (dialog, args) =>
                    {
                        s_logger.Info("用户拒绝了隐私政策");
                        App.Current.Shutdown();
                    };
                    await cd.ShowAsync();
                }
            }
            catch (Exception ex)
            {
                s_logger.Error("检查公告失败：" + ex.Message + "\n" + ex.StackTrace);
                await ShowDialog("确认", "检查公告失败：" + ex.Message + "\n" + ex.StackTrace, "错误");
            }


            try
            {
                s_logger.Info("正在检查公告");
                string noticeFile = await HttpUtils.GetString("https://huangyu.win/LMC/notice.line");
                s_logger.Info("获取到的公告文件: \n" + noticeFile);
                File.WriteAllText("./LMC/temp/notice.line", noticeFile);
                var lfp = new LineFileParser();
                string id = lfp.Read("./LMC/temp/notice.line", "noticeId", "notice");
                if (Config.ReadGlobal("notice", "id") != id)
                {
                    s_logger.Info("公告未被查看，V: " + Config.ReadGlobal("notice", "id") + "/" + id);
                    Config.WriteGlobal("notice", "id", id);
                    string content = lfp.Read("./LMC/temp/notice.line", "noticeContent", "notice");
                    await ShowDialog("确认", content, "公告", ContentDialogButton.Close);
                }
            }
            catch (Exception ex)
            {
                s_logger.Error("检查公告失败：" + ex.Message + "\n" + ex.StackTrace);
                await ShowDialog("确认", "检查公告失败：" + ex.Message + "\n" + ex.StackTrace, "错误");
            }

            if (Logger.DebugMode)
            {

                EnqueueMessage(new InfoBarMessage("调试模式已启用，将输出调试日志，可以在 设置 - 调试设置 中关闭。", InfoBarSeverity.Informational, "调试模式"));
            }
        }


        public static void AddAccPage()
        {
            Navigate(AddAccountPage);
        }

        public static async Task<ContentDialogResult> ShowDialog(string closeButtonText, string content, string title,
            ContentDialogButton defaultButton = ContentDialogButton.None, string primaryButtonText = null, string secondaryButtonText = null)
        {
            short temp = (short)new Random().Next(1000, 9999);
            s_logger.Info(
                $"正在显示Dialog{temp}：\nContent: {content}\nTitle: {title}\nSecButton: {secondaryButtonText}\nPrimButton: {primaryButtonText}\nDef: {defaultButton}\nClose: {closeButtonText}");
            var dialog = new ContentDialog();
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
                dialog.SecondaryButtonText = secondaryButtonText;
                dialog.CloseButtonText = closeButtonText;
            }

            dialog.DefaultButton = defaultButton == ContentDialogButton.Close && secondaryButtonText == null ? ContentDialogButton.Secondary : defaultButton;
            var res = await dialog.ShowAsync();
            res = res == ContentDialogResult.Secondary && secondaryButtonText == null ? ContentDialogResult.None : res;
            s_logger.Info($"Dialog{temp}的用户操作为{res}");
            return res;
        }

        public static void Navigate(Page page, bool push = true) 
        {
            if (MainFrame.Content != null && MainFrame.Content is Page)
            {
                if (push && !(MainFrame.Content is ProfileManagePage))
                {
                    s_accessPages.Push(MainFrame.Content as Page);
                    s_lastSelectedItem.Push(MainNagView.SelectedItem as NavigationViewItem);
                }
            }

            MainNagView.Header = page.Title;
            MainFrame.Navigate(page);
        }

        private void NavigationView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
        {
            Page? page = null;
            object item = args.SelectedItem;
            if (item == HomeNagVi)
            {
                page = HomePage;
            }
            else if (item == DownloadNagVi)
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
            else if (item == TaskNagVi)
            {
                page = DownloadPage;
            }
            else if (item == SettingNagVi)
            {
                page = SettingPage;
            }

            if (page != null)
            {
                Navigate(page);
            }
        }

        public static void ShowMsgBox(string title, string content, Action action = null)
        {
            s_logger.Info("正在显示MsgBox：" +
                          $"\nTitle: {title}" +
                          $"\nContent: {content}" +
                          $"\nAction: {action.GetType()}");
            var msgButton = MessageBoxButton.OK;
            var res = MessageBox.Show(content, title, msgButton);
            if (action != null)
            {
                action.Invoke();
            }
        }

        public static void ChangeTaskInfoBadge(int i)
        {
            s_taskInfoBadge.Value += i;
        }

        public void InfoBarMessageService(params InfoBar[] infoBars)
        {
            _infoBars = infoBars;

            // 初始化InfoBar状态并订阅关闭事件
            foreach (var bar in _infoBars)
            {
                bar.IsOpen = false;
                bar.Closed += (sender, e) => OnInfoBarClosed();
            }
        }

        public void EnqueueMessage(InfoBarMessage message)
        {
            lock (_syncRoot)
            {
                // 尝试立即显示消息
                if (TryFindAvailableBar(out var availableBar))
                {
                    ShowMessage(availableBar, message);
                    return;
                }

                // 无可用InfoBar时入队
                _messageQueue.Enqueue(message);
            }
        }

        private void OnInfoBarClosed()
        {
            lock (_syncRoot)
            {
                // 当有InfoBar关闭时尝试显示下一条消息
                if (_messageQueue.TryDequeue(out var message) &&
                    TryFindAvailableBar(out var availableBar))
                {
                    ShowMessage(availableBar, message);
                }
            }
        }

        private bool TryFindAvailableBar(out InfoBar availableBar)
        {
            foreach (var bar in _infoBars)
                if (!bar.IsOpen)
                {
                    availableBar = bar;
                    return true;
                }

            availableBar = null;
            return false;
        }

        private void ShowMessage(InfoBar infoBar, InfoBarMessage message)
        {
            s_logger.Info($"显示消息：{message.Message}:\n{message.Severity}\n{message.Message}");
            infoBar.Message = message.Message;
            infoBar.Title = message.Title;
            infoBar.Severity = message.Severity;
            infoBar.IsOpen = true;

            var autoCloseTimer = new Timer(3000);
            autoCloseTimer.Elapsed += (s, e) =>
            {
                autoCloseTimer.Dispose();
                Dispatcher.Invoke(() => infoBar.IsOpen = false);
            };
            autoCloseTimer.Start();
        }
    }

    public class InfoBarMessage
    {

        public InfoBarMessage(string message, InfoBarSeverity severity, string title)
        {
            Message = message;
            Severity = severity;
            Title = title;
        }

        public string Message { get; set; }
        public InfoBarSeverity Severity { get; set; }
        public string Title { get; set; }
    }
}