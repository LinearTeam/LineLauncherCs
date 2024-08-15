using LMC.Basic;
using LMC.Minecraft;
using System;
using System.IO;
using System.Net;
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
            string path = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + "/.linelauncher/i18n/";
            try
            {
                Directory.CreateDirectory (path);
                using (WebClient client = new WebClient())
                {
                    client.DownloadFile("https://huangyu.win/LMC/en_US.line", $"{path}en_US.line");
                    client.DownloadFile("https://huangyu.win/LMC/zh_CN.line", $"{path}zh_CN.line");
                }
            }
            catch(Exception e)
            {
                logger.warn("Failed to download i18N files cause:\n" + e.Message + "\n, checking local...");
                if(File.Exists(path + i18NTools.getLangName() + ".line"))
                {
                    logger.error("Failed to load local's i18N file");
                    throw new Exception("无法下载/找到语言文件|Can't find or download i18n files");
                }
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
        
        private void homevi_Selected(object sender, RoutedEventArgs e)
        {

        }
    }
}
