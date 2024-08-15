using LMC.Basic;
using System;
using System.Windows;

namespace LMC
{
    /// <summary>
    /// App.xaml 的交互逻辑
    /// </summary>
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
            DispatcherUnhandledException += App_DispatcherUnhandledException;
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
            MessageBox.Show($"发生了一个致命错误，点击确认按钮打开反馈页面进行反馈/An unexcepted error occurred， click 'Confirm' button to send us feedback:\n{ex.InnerException.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            Logger logger = new Logger("A");
            logger.error(ex.Message + "\n" + ex.StackTrace);
            System.Diagnostics.Process.Start("explorer.exe", "\"https://github.com/IceCreamTeamICT/LineLauncherCs/issues/new/choose\"");
        }
    }
}
