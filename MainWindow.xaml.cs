using LMC.Account.OAuth;
using LMC.Basic;
using LMC.Minecraft;
using LMC.Pages;
using System;
using System.IO;
using System.Net;
using System.Security.Policy;
using System.Windows;
using System.Windows.Controls;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;

namespace LMC
{

    /// <summary>
    /// MainWindow.xaml 的交互逻辑
    /// </summary>
    public partial class MainWindow : Window
    {
        public static InfoBar infobar;
        public static ContentPresenter cdp;
        public static NavigationView mnv;
        HomePage homep = new HomePage();
        public static i18nTools i18NTools = new i18nTools();
        GameDownload GameDownload = new GameDownload();
        Logger logger;
        LineFileParser lfp = new LineFileParser();
        public MainWindow()
        {
            Directory.CreateDirectory("./lmc/");
            Directory.CreateDirectory("./lmc/logs");
            File.Create("./lmc/logs/latest.log").Close();
            if (string.IsNullOrEmpty(lfp.ReadFile("./lmc/main.line", "logN", "main"))){
                Logger.logNum = "1";
                lfp.WriteFile("./lmc/main.line", "logN", "1", "main");
            }   
            else
            {
                if (int.Parse(lfp.ReadFile("./lmc/main.line", "logN", "main")) != 5)
                {
                    string logN = (int.Parse(lfp.ReadFile("./lmc/main.line", "logN", "main")) + 1).ToString();
                    Logger.logNum = logN;
                    lfp.WriteFile("./lmc/main.line", "logN", logN, "main");
                }
                else
                {
                    Logger.logNum = "1";
                    lfp.WriteFile("./lmc/main.line", "logN", "1", "main");
                }
            }
            
            logger = new Logger("MainUI");
            logger.info("MainWindow Open");
            logger.info("Downloading i18N Files...");
            using(WebClient client = new WebClient())
            {
                client.DownloadFile("https://huangyu.win/LMC/en_US.line", "./lmc/resources/i18n/en_US.line");
                client.DownloadFile("https://huangyu.win/LMC/zh_CN.line", "./lmc/resources/i18n/zh_CN.line");
            }
            Directory.CreateDirectory(GameDownload.gamePath);
            SystemThemeWatcher.Watch(this);
            ApplicationThemeManager.Apply(ApplicationTheme.Dark);
            InitializeComponent();
            refreshUiContent();
        }
        private void WindowRendered(object sender, EventArgs e)
        {
            Window window = new i18nEditWindow();
            window.Owner = this;
            window.Show();
            mnv = nagv;
            nagv.Navigate(typeof(LMC.HomePage));
            cdp = ContentDialogPresenter;
            infobar = ib;
        }
        private void refreshUiContent()
        {
            try
            {
                homevi.Content = i18NTools.getString(homevi.Content.ToString());
                settingvi.Content = i18NTools.getString(settingvi.Content.ToString());
                accountvi.Content = i18NTools.getString(accountvi.Content.ToString());
                downloadvi.Content = i18NTools.getString(downloadvi.Content.ToString());
                othervi.Content = i18NTools.getString(othervi.Content.ToString());
                if (i18NTools.getLangName().Equals("en_US"))
                {
                    homevi.FontFamily = new System.Windows.Media.FontFamily("Microsoft Yi Baiti");
                    settingvi.FontFamily = new System.Windows.Media.FontFamily("Microsoft Yi Baiti");
                    accountvi.FontFamily = new System.Windows.Media.FontFamily("Microsoft Yi Baiti");
                    downloadvi.FontFamily = new System.Windows.Media.FontFamily("Microsoft Yi Baiti");
                    othervi.FontFamily = new System.Windows.Media.FontFamily("Microsoft Yi Baiti");
                }
            }
            catch(Exception e)
            {
                //TODO: tell user
                logger.error($"An error catched when refreshing ui content {e.ToString()}");
                return;
            }

        }

        private void homevi_Selected(object sender, RoutedEventArgs e)
        {

        }
    }
}
