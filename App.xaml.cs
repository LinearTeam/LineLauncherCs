using LMC.Basic;
using System;
using System.IO;
using System.IO.Compression;
using System.Windows;
using MessageBox = iNKORE.UI.WPF.Modern.Controls.MessageBox;

namespace LMC
{
    /// <summary>
    /// App.xaml 的交互逻辑
    /// </summary>
    public partial class App : Application
    {
        private LineFileParser _lineFileParser = new LineFileParser();

        public static string LauncherVersion = "2.0.0";
        public static string LauncherVersionType = "alpha";
        public static string LauncherBuildVersion = "0003";

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            Directory.CreateDirectory("./LMC/");
            Directory.CreateDirectory("./LMC/logs");
            File.Create("./LMC/logs/latest.log").Close();
            if (string.IsNullOrEmpty(_lineFileParser.Read("./LMC/main.line", "logN", "main")))
            {
                Logger.LogNum = "1";
                _lineFileParser.Write("./LMC/main.line", "logN", "1", "main");
            }
            else
            {
                if (int.Parse(_lineFileParser.Read("./LMC/main.line", "logN", "main")) != 5)
                {
                    string logN = (int.Parse(_lineFileParser.Read("./LMC/main.line", "logN", "main")) + 1).ToString();
                    Logger.LogNum = logN;
                    _lineFileParser.Write("./LMC/main.line", "logN", logN, "main");
                }
                else
                {
                    Logger.LogNum = "1";
                    _lineFileParser.Write("./LMC/main.line", "logN", "1", "main");
                }
            }



            Logger logger = new Logger("A");
            logger.Info($"日志记录开始 程序版本: {LauncherVersion} 记录器版本: {Logger.LoggerVersion} 日志编号: {Logger.LogNum} ，正在初始化程序");

            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
            DispatcherUnhandledException += App_DispatcherUnhandledException;
        }

        protected override void OnExit(ExitEventArgs e)
        {
            Logger logger = new Logger("A");
            logger.Info($"日志记录结束 程序版本: {LauncherVersion} 记录器版本: {Logger.LoggerVersion} 日志编号: {Logger.LogNum} ，正在退出程序");
            base.OnExit(e);
        }

        private void App_DispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
        {
            ShowException(e.Exception);
            e.Handled = true; 
        }

        private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            ShowException(e.ExceptionObject as Exception);
        }

        private void ShowException(Exception ex)
        {
            Logger logger = new Logger("A");
            logger.Error(ex.Message + "\n" + ex.StackTrace);
            LMC.MainWindow.ShowMsgBox("错误", $"发生了一个致命错误，点击确认按钮打开反馈页面进行反馈/An unexcepted error occurred， click 'Confirm' button to send us feedback:\n{ex.Message}\n{ex.StackTrace}", () =>
            {
                System.Diagnostics.Process.Start("explorer.exe", "\"https://github.com/IceCreamTeamICT/LineLauncherCs/issues/new/choose\"");
                try
                {
                    Directory.CreateDirectory("./LMC/");
                    Directory.CreateDirectory("./LMC/empty");
                    Directory.Delete("./LMC/empty", true);
                    Directory.CreateDirectory("./LMC/empty");
                    if (File.Exists("./LMC/FeedbackPack_反馈包_PleaseUpload_请上传.zip"))
                    {
                        File.Delete("./LMC/FeedbackPack_反馈包_PleaseUpload_请上传.zip");
                    }
                    ZipFile.CreateFromDirectory("./LMC/empty", "./LMC/FeedbackPack_反馈包_PleaseUpload_请上传.zip");
                    Directory.Delete("./LMC/empty", true);
                    using (FileStream zipToOpen = new FileStream("./LMC/FeedbackPack_反馈包_PleaseUpload_请上传.zip", FileMode.Open))
                    {
                        using (ZipArchive archive = new ZipArchive(zipToOpen, ZipArchiveMode.Update))
                        {
                            for (int i = 1; i <= 5; i++)
                            {
                                if (File.Exists($"./LMC/logs/log{i}.log"))
                                {
                                    ZipArchiveEntry logEntry = archive.CreateEntry($"logs/log{i}.log");
                                    using (StreamWriter writer = new StreamWriter(logEntry.Open()))
                                    {
                                        var log = File.ReadAllText($"./LMC/logs/log{i}.log");
                                        writer.Write(log);
                                    }
                                }

                            }
                            ZipArchiveEntry entry = archive.CreateEntry($"logs/latest.log");
                            using (StreamWriter writer = new StreamWriter(entry.Open()))
                            {
                                var log = File.ReadAllText($"./LMC/logs/latest.log");
                                writer.Write(log);
                            }
                            entry = archive.CreateEntry($"logs/crash_report.log");
                            using (StreamWriter writer = new StreamWriter(entry.Open()))
                            {
                                writer.WriteLine("LMC Crashed.");
                                writer.WriteLine($"Caused By: {ex.Message}");
                                writer.WriteLine($"StackTrace: \n{ex.StackTrace}");
                                writer.WriteLine(ex.InnerException == null ? "No InnerException" : $"InnerException: {ex.InnerException.Message}\nStackTrace: \n{ex.InnerException.StackTrace}");
                                writer.WriteLine($"Launcher Version: {LauncherVersion} Logger Version: {Logger.LoggerVersion}");
                            }
                            entry = archive.CreateEntry($"logs/exception_object.log");
                            using (StreamWriter writer = new StreamWriter(entry.Open()))
                            {
                                writer.Write(ex);
                            }
                        }
                    }
                    System.Diagnostics.Process.Start("explorer", "/select," + Directory.GetCurrentDirectory() + @"\LMC\FeedbackPack_反馈包_PleaseUpload_请上传.zip");
                }
                catch{}
            });
        }
    }
}
