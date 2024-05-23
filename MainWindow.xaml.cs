using LMC.Basic;
using LMC.Minecraft;
using System;
using System.IO;
using System.Windows;
using Wpf.Ui.Appearance;

namespace LMC
{
    /// <summary>
    /// MainWindow.xaml 的交互逻辑
    /// </summary>
    public partial class MainWindow : Window
    {
        HomePage homep = new HomePage();
        i18nTools i18NTools = new i18nTools();
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
            //TODO: select homevi default
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
                //TODO: log error and tell user
                return;
            }

        }

    }
}
