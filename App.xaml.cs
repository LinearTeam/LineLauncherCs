using LMC.Basic;
using System;
using System.IO;
using System.Windows;

namespace LMC
{
    /// <summary>
    /// App.xaml 的交互逻辑
    /// </summary>
    public partial class App : Application
    {
                private LineFileParser _lineFileParser = new LineFileParser();
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
            logger.Info("日志记录开始 日志编号:" + Logger.LogNum + "，正在初始化程序");

            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
            DispatcherUnhandledException += App_DispatcherUnhandledException;
        }

        protected override void OnExit(ExitEventArgs e)
        {
            Logger logger = new Logger("A");
            logger.Info("日志记录结束 日志编号:" + Logger.LogNum + "，正在退出程序");
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
            MessageBox.Show($"发生了一个致命错误，点击确认按钮打开反馈页面进行反馈/An unexcepted error occurred， click 'Confirm' button to send us feedback:\n{ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            System.Diagnostics.Process.Start("explorer.exe", "\"https://github.com/IceCreamTeamICT/LineLauncherCs/issues/new/choose\"");
        }
    }
}
