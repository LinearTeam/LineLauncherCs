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
        public static (List<VersionDownload> normal, List<VersionDownload> alpha, List<VersionDownload> beta) versions;
        public DownloadPage()
        {
            InitializeComponent();
            refreshContent();
        }
        async public Task ParseManifest()
        {
            GameDownload gameDownload = new GameDownload();
            gameDownload.BMCLAPI();
            versions = gameDownload.ManifestParse(await gameDownload.GetVersionManifest());
        }
        async public Task refreshContent()
        {
            confirm.Content = MainWindow.i18NTools.getString(confirm.Content.ToString());
            rel.Content = MainWindow.i18NTools.getString(rel.Content.ToString());
            pre.Content = MainWindow.i18NTools.getString(pre.Content.ToString()); 
            old.Content = MainWindow.i18NTools.getString(old.Content.ToString());
            //spe.Content = MainWindow.i18NTools.getString(spe.Content.ToString());
            if (MainWindow.i18NTools.getLangName().Equals("en_US"))
            {
                confirm.FontFamily = new System.Windows.Media.FontFamily("Microsoft Yi Baiti");
                rel.FontFamily = new System.Windows.Media.FontFamily("Microsoft Yi Baiti");
                pre.FontFamily = new System.Windows.Media.FontFamily("Microsoft Yi Baiti");
                old.FontFamily = new System.Windows.Media.FontFamily("Microsoft Yi Baiti");
                //spe.FontFamily = new System.Windows.Media.FontFamily("Microsoft Yi Baiti");
                vlist.FontFamily = new System.Windows.Media.FontFamily("Microsoft Yi Baiti");
            }
            await ParseManifest();    
        }
        private void refreshVlist()
        {
            bool isOld = (bool)old.IsChecked;
            bool isPre = (bool)pre.IsChecked;
            bool isRel = (bool)rel.IsChecked;
            List<VersionDownload> res = new List<VersionDownload>();
            foreach (VersionDownload ver in versions.normal)
            {
                if (isRel && ver.type == 0)
                {
                    res.Add(ver);
                }
                else if (isPre && ver.type == 1)
                {
                    res.Add(ver);
                }
            }
            if (isOld)
            {
                foreach (VersionDownload ver in versions.beta)
                {
                    res.Add(ver);
                }
                foreach (VersionDownload ver in versions.alpha)
                {
                    res.Add(ver);
                }
            }
            vlist.Items.Clear();
            foreach(VersionDownload ver in res)
            {
                vlist.Items.Add(ver.id);
            }
        }

        private void old_Checked(object sender, RoutedEventArgs e)
        {
            refreshVlist();
        }

        private void confirm_Click(object sender, RoutedEventArgs e)
        {
            MainWindow.mnv.Navigate(typeof(DownloadCurrentVersionPage));
        }
    }
}
