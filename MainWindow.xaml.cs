using LMC.Basic;
using LMC.Minecraft;
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
        }


    }
}
