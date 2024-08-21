using LMC.Minecraft;
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

namespace LMC.Pages
{
    /// <summary>
    /// DownloadCurrentVersionPage.xaml 的交互逻辑
    /// </summary>
    public partial class DownloadCurrentVersionPage : Page
    {
        public DownloadCurrentVersionPage()
        {
            InitializeComponent();
            refreshContent();
        }
        public static string CurrentVersion = "1.21";
        private void back_Click(object sender, RoutedEventArgs e)
        {
            MainWindow.mnv.Navigate(typeof(DownloadPage));
        }

        async private void refreshContent()
        {
            confirm.Content = MainWindow.i18NTools.getString(confirm.Content.ToString());
            dvTitle.Text = MainWindow.i18NTools.getString(dvTitle.Text);
            if (MainWindow.i18NTools.getLangName().ToLower().Equals("en_us"))
            {
                dvTitle.FontFamily = new System.Windows.Media.FontFamily("Microsoft Yi Baiti");
                confirm.FontFamily = new System.Windows.Media.FontFamily("Microsoft Yi Baiti");
                forge.FontFamily = new System.Windows.Media.FontFamily("Microsoft Yi Baiti");
                fab.FontFamily = new System.Windows.Media.FontFamily("Microsoft Yi Baiti");
                opt.FontFamily = new System.Windows.Media.FontFamily("Microsoft Yi Baiti");
            }
            GameDownload gd = new GameDownload();
            gd.BMCLAPI();
            var a = await gd.GetForgeFabricOptifineVersionList(CurrentVersion);
            forge.Items.Clear();
            fab.Items.Clear();
            opt.Items.Clear();
            string ff = MainWindow.i18NTools.getString("lmc.pages.dcp.failed").Replace("{type}","Forge");
            string of = MainWindow.i18NTools.getString("lmc.pages.dcp.failed").Replace("{type}", "Optifine");
            string bf = MainWindow.i18NTools.getString("lmc.pages.dcp.failed").Replace("{type}", "Fabric");
            if (a.fabs.First().Equals("N"))
            {
                bf = MainWindow.i18NTools.getString("lmc.pages.dcp.notsupp").Replace("{type}", "Fabric");
            }
            if (a.forges.First().Equals("N"))
            {
                ff = MainWindow.i18NTools.getString("lmc.pages.dcp.notsupp").Replace("{type}", "Forge");
            }
            if (a.opts.First().Equals("N"))
            {
                of = MainWindow.i18NTools.getString("lmc.pages.dcp.notsupp").Replace("{type}", "Optifine");
            }
            foreach ( string f in a.forges ) 
            {
                if (f.Equals("N")) continue;
                forge.Items.Add(f);
                ff = MainWindow.i18NTools.getString("lmc.pages.dcp.plzc").Replace("{type}", "Forge");
            }
            foreach (string f in a.fabs)
            {
                if (f.Equals("N")) continue;
                fab.Items.Add(f);
                bf = MainWindow.i18NTools.getString("lmc.pages.dcp.plzc").Replace("{type}", "Fabric");
            }
            foreach (string f in a.opts)
            {
                if (f.Equals("N")) continue;
                opt.Items.Add(f);
                of = MainWindow.i18NTools.getString("lmc.pages.dcp.plzc").Replace("{type}", "Optifine");
            }
            forge.Items.Insert(0,ff);
            fab.Items.Insert(0,bf);
            opt.Items.Insert(0, of);
            forge.SelectedItem = forge.Items.GetItemAt(0);
            fab.SelectedItem = fab.Items.GetItemAt(0);
            opt.SelectedItem = opt.Items.GetItemAt(0);
        }
        bool isForge = false;
        bool isOpt = false;
        bool isFab = false;
        async private Task optTip()
        {
            await Task.Run(() => Console.WriteLine("Hello World!"));
        }
        async private void opt_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            isOpt = opt.SelectedIndex != 0;
            if (isOpt && isFab) await optTip();
        }
        
        async private void fab_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            isFab = fab.SelectedIndex != 0;
            if (isOpt && isFab) await optTip();
            if (isFab) forge.IsEnabled = false;
        }

        private void forge_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (isFab) return;
            isForge = forge.SelectedIndex != 0;
            if(isForge) fab.IsEnabled = false;
        }

        private void confirm_Click(object sender, RoutedEventArgs e)
        {

        }
    }
}
