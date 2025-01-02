using Common;
using LMC.Basic;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Windows;
using Newtonsoft.Json;


namespace LMC
{
    /// <summary>
    /// App.xaml 的交互逻辑
    /// </summary>
    public partial class App : Application
    {
        private LineFileParser _lineFileParser = new LineFileParser();

        public static string LauncherVersion = "2.0.2";
        public static string LauncherVersionType = "alpha";
        public static string LauncherBuildVersion = "0006";
        public static string LauncherUpdateLog =
@"    新功能：
        ✨ 添加离线账号
        ✨ 按下ESC时返回上一页 (有与UI库相关的问题)
        ✨ 这个对话框下面的打开GitHub按钮
        ✨ 在存储窗口大小的同时存储最大化状态
    修复BUG：
        🐛 获取头像BUG (目前仍有部分问题)
        🐛 部分环境无法正常显示主题
        🐛 更新脚本无法正常运行
        🐛 反馈包无法正常定位
        🐛 修复删除账号时会下载失败的BUG
    其他：
        ✨ 修改请求UA头
    ......
    详细内容查看GitHub
";

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
            logger.Info($"日志记录开始 程序版本: {LauncherVersion} 构建号: {LauncherBuildVersion} 版本类型: {LauncherVersionType} 记录器版本: {Logger.LoggerVersion} 日志编号: {Logger.LogNum} ，正在初始化程序");
            
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
            DispatcherUnhandledException += App_DispatcherUnhandledException;
        }

        protected override void OnExit(ExitEventArgs e)
        {
            Logger logger = new Logger("A");
            logger.Info($"日志记录结束 程序版本: {LauncherVersion} 构建号: {LauncherBuildVersion} 版本类型: {LauncherVersionType} 记录器版本: {Logger.LoggerVersion} 日志编号: {Logger.LogNum} ，正在退出程序");
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
                            entry = archive.CreateEntry($"info/crash_report.cr");
                            using (StreamWriter writer = new StreamWriter(entry.Open()))
                            {
                                writer.WriteLine("LMC Crashed.");
                                var list = new List<string>();
                                list.Add("Let's speak English");
                                list.Add("You can try Loser Launcher Maybe?");
                                list.Add("WHAT THE FUCK");
                                list.Add("Okay, let's use WinUi 3 and go to the hell");
                                list.Add("Delete all files of LMC and be careful with your .minecraft folder.");
                                list.Add("让我们说中文");
                                list.Add("你或许可以试试LoserMC？");
                                list.Add("他妈的");
                                list.Add("ok，让我们使用胜利UI3并下地狱");
                                list.Add("删除LMC的所有文件并小心你的.地雷手艺文件夹");
                                writer.WriteLine(list[new Random().Next(0, list.Count)]);
                                writer.WriteLine($"Caused By: {ex.Message}");
                                writer.WriteLine($"StackTrace: \n{ex.StackTrace}");
                                writer.WriteLine(ex.InnerException == null ? "No InnerException" : $"InnerException: {ex.InnerException.Message}\nStackTrace: \n{ex.InnerException.StackTrace}");
                                writer.WriteLine($"Launcher Version: {LauncherVersion} BuildNumber: {LauncherBuildVersion} VersionType: {LauncherVersionType} Logger Version: {Logger.LoggerVersion}");
                            }
                            entry = archive.CreateEntry($"info/exception_object.o");
                            using (StreamWriter writer = new StreamWriter(entry.Open()))
                            {
                                writer.Write(ex.ToJson());
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
