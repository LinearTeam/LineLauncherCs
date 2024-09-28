using LMC.Basic;
using LMC.Minecraft;
using LMC.Pages;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace LMC
{
    /// <summary>
    /// DownloadPage.xaml 的交互逻辑
    /// </summary>
    public partial class DownloadPage : Page
    {
        private static Logger s_logger = new Logger("DP");
        public static (List<DVersion> normal, List<DVersion> alpha, List<DVersion> beta) Versions;
        public DownloadPage()
        {
            InitializeComponent();
        }
        public async static Task ParseManifest()
        {
            Versions = MainWindow.GameDownloader.ParseManifest(await MainWindow.GameDownloader.GetVersionManifest());
        }
        async public Task RefreshContent()
        {
/*            confirm.Content = MainWindow.I18NTools.getString(confirm.Content.ToString());
            rel.Content = MainWindow.I18NTools.getString(rel.Content.ToString());
            pre.Content = MainWindow.I18NTools.getString(pre.Content.ToString()); 
            old.Content = MainWindow.I18NTools.getString(old.Content.ToString());
            //spe.Content = MainWindow.i18NTools.getString(spe.Content.ToString());
            if (MainWindow.I18NTools.getLangName().Equals("en_US"))
            {
                confirm.FontFamily = new System.Windows.Media.FontFamily("Microsoft Yi Baiti");
                rel.FontFamily = new System.Windows.Media.FontFamily("Microsoft Yi Baiti");
                pre.FontFamily = new System.Windows.Media.FontFamily("Microsoft Yi Baiti");
                old.FontFamily = new System.Windows.Media.FontFamily("Microsoft Yi Baiti");
                //spe.FontFamily = new System.Windows.Media.FontFamily("Microsoft Yi Baiti");
                VersionList.FontFamily = new System.Windows.Media.FontFamily("Microsoft Yi Baiti");
            }
  */
            await ParseManifest();    
        }
        private async Task RefreshVersionList()
        {
            bool isOld = (bool)old.IsChecked;
            bool isPre = (bool)pre.IsChecked;
            bool isRel = (bool)rel.IsChecked;
            List<DVersion> res = new List<DVersion>();
            if(Versions.normal == null || Versions.beta == null || Versions.alpha == null) await ParseManifest();
            foreach (DVersion ver in Versions.normal)
            {
                if (isRel && ver.Type == 0)
                {
                    res.Add(ver);
                }
                else if (isPre && ver.Type == 1)
                {
                    res.Add(ver);
                }
            }
            if (isOld)
            {
                foreach (DVersion ver in Versions.beta)
                {
                    res.Add(ver);
                }
                foreach (DVersion ver in Versions.alpha)
                {
                    res.Add(ver);
                }
            }
            VersionList.Items.Clear();
            foreach(DVersion ver in res)
            {
                VersionList.Items.Add(ver.Id);
            }
        }

        async private void old_Checked(object sender, RoutedEventArgs e)
        {
            int attempt = 0;
            string errorMsg = string.Empty;
            while (attempt <= 10)
            {
                try
                {
                    await RefreshVersionList();
                    return;
                }
                catch(Exception ex) { 
                    attempt++; 
                    errorMsg = ex.Message;
                    s_logger.Warn($"无法加载原版版本列表{ex.Message}\n{ex.StackTrace}\n 重试次数 {attempt}.");
                }
            }
            await MainWindow.ShowMsgBox("错误",$"似乎在加载版本列表时发生了错误，信息如下：\n{errorMsg}\n请检查网络连接或尝试切换下载源，若仍然出现此问题请在 其他 -> 反馈 中进行反馈。"  ,"继续");
        }

        private void confirm_Click(object sender, RoutedEventArgs e)
        {
            if(VersionList.SelectedItem != null){
                DownloadCurrentVersionPage.CurrentVersion = (string) VersionList.SelectedItem;
                MainWindow.MainNagView.Navigate(typeof(DownloadCurrentVersionPage));
            }
        }
    }
}
