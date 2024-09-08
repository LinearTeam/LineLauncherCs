using LMC.Basic;
using LMC.Minecraft;
using System;
using System.IO;
using System.Net;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Wpf.Ui;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;
using Wpf.Ui.Extensions;

namespace LMC
{

    /// <summary>
    /// MainWindow.xaml 的交互逻辑
    /// </summary>
    public partial class MainWindow : Window
    {
        
        public static InfoBar InfoBar;
        public static ContentPresenter ContentPre;
        public static NavigationView MainNagView;
        public static I18nTools I18NTools = new I18nTools();
        private Logger _logger;
        private LineFileParser _lineFileParser = new LineFileParser();
        public MainWindow()
        {
            Directory.CreateDirectory("./lmc/");
            Directory.CreateDirectory("./lmc/logs");
            File.Create("./lmc/logs/latest.log").Close();
            if (string.IsNullOrEmpty(_lineFileParser.Read("./lmc/main.line", "logN", "main"))){
                Logger.LogNum = "1";
                _lineFileParser.Write("./lmc/main.line", "logN", "1", "main");
            }   
            else
            {
                if (int.Parse(_lineFileParser.Read("./lmc/main.line", "logN", "main")) != 5)
                {
                    string logN = (int.Parse(_lineFileParser.Read("./lmc/main.line", "logN", "main")) + 1).ToString();
                    Logger.LogNum = logN;
                    _lineFileParser.Write("./lmc/main.line", "logN", logN, "main");
                }
                else
                {
                    Logger.LogNum = "1";
                    _lineFileParser.Write("./lmc/main.line", "logN", "1", "main");
                }
            }
            
            _logger = new Logger("MainUI");
            _logger.Info("MainWindow Open");
            /*
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
                if(File.Exists(path + I18NTools.getLangName() + ".line"))
                {
                    logger.error("Failed to load local's i18N file");
                    throw new Exception("无法下载/找到语言文件|Can't find or download i18n files");
                }
            }
            */
            Directory.CreateDirectory(GameDownloader.GamePath);
            SystemThemeWatcher.Watch(this);
            ApplicationThemeManager.Apply(ApplicationTheme.Dark);
            InitializeComponent();
            //refreshUiContent();
        }

        private void WindowRendered(object sender, EventArgs e)
        {
            Window window = new i18nEditWindow();
            window.Owner = this;
            window.Show();
            MainNagView = nagv;
            nagv.Navigate(typeof(LMC.HomePage));
            ContentPre = ContentDialogPresenter;
            InfoBar = ib;
        }
        /*
        private void refreshUiContent()
        {
            homevi.Content = I18NTools.getString(homevi.Content.ToString());
            settingvi.Content = I18NTools.getString(settingvi.Content.ToString());
            accountvi.Content = I18NTools.getString(accountvi.Content.ToString());
            downloadvi.Content = I18NTools.getString(downloadvi.Content.ToString());
            othervi.Content = I18NTools.getString(othervi.Content.ToString());
            if (I18NTools.getLangName().Equals("en_US"))
            {
                homevi.FontFamily = new System.Windows.Media.FontFamily("Microsoft Yi Baiti");
                settingvi.FontFamily = new System.Windows.Media.FontFamily("Microsoft Yi Baiti");
                accountvi.FontFamily = new System.Windows.Media.FontFamily("Microsoft Yi Baiti");
                downloadvi.FontFamily = new System.Windows.Media.FontFamily("Microsoft Yi Baiti");
                othervi.FontFamily = new System.Windows.Media.FontFamily("Microsoft Yi Baiti");
            }
            
        }
        */
        async public static Task ShowMsgBox(string title,string content, string button)
        {
            var confirmDialog = new ContentDialog(MainWindow.ContentPre);
  /*          if (I18NTools.getLangName().Equals("en_US"))
            {
                confirmDialog.SetCurrentValue(ContentDialog.FontFamilyProperty, new System.Windows.Media.FontFamily("Microsoft Yi Baiti"));
            }
  */          confirmDialog.SetCurrentValue(ContentDialog.IsPrimaryButtonEnabledProperty, false);
            confirmDialog.SetCurrentValue(ContentDialog.IsSecondaryButtonEnabledProperty, false);
            confirmDialog.SetCurrentValue(ContentDialog.TitleProperty, title);
            confirmDialog.SetCurrentValue(ContentProperty, content);
            confirmDialog.SetCurrentValue(ContentDialog.CloseButtonTextProperty, button);
            await confirmDialog.ShowAsync();

        }
        async public static Task ShowMsgBox(string title,string content,string confirm,string cancel, TypedEventHandler<ContentDialog, ContentDialogButtonClickEventArgs> primaryButtonClicked)
        {
            var confirmDialog = new ContentDialog(ContentPre);
/*            if (I18NTools.getLangName().Equals("en_US"))
            {
                confirmDialog.SetCurrentValue(ContentDialog.FontFamilyProperty, new System.Windows.Media.FontFamily("Microsoft Yi Baiti"));
            }
*/            confirmDialog.SetCurrentValue(ContentDialog.IsPrimaryButtonEnabledProperty, true);
            confirmDialog.SetCurrentValue(ContentDialog.IsSecondaryButtonEnabledProperty,false);
            confirmDialog.SetCurrentValue(ContentDialog.PrimaryButtonTextProperty,confirm);
            confirmDialog.SetCurrentValue(ContentDialog.CloseButtonTextProperty,cancel);
            confirmDialog.SetCurrentValue(ContentDialog.TitleProperty,title);
            confirmDialog.SetCurrentValue(ContentProperty, content);
            confirmDialog.ButtonClicked += primaryButtonClicked;
            await confirmDialog.ShowAsync();
        }
    }
}
