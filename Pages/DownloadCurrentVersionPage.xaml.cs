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

        private void back_Click(object sender, RoutedEventArgs e)
        {
            MainWindow.mnv.Navigate(typeof(DownloadPage));
        }

        async private void refreshContent()
        {
            GameDownload gd = new GameDownload();
            gd.BMCLAPI();
            var a = await gd.GetForgeFabricOptifineVersionList("1.14.4");
            forge.Items.Clear();
            fab.Items.Clear();
            opt.Items.Clear();
            forge.Items.Add("请选择Forge版本");
            fab.Items.Add("请选择Fabric版本");
            opt.Items.Add("请选择Optifine版本");
            foreach ( string f in a.forges ) { 
                forge.Items.Add(f);
            }
            foreach (string f in a.fabs)
            {
                fab.Items.Add(f);
            }
            foreach (string f in a.opts)
            {
                opt.Items.Add(f);
            }
            forge.SelectedItem = forge.Items.GetItemAt(0);
            fab.SelectedItem = forge.Items.GetItemAt(0);
            opt.SelectedItem = forge.Items.GetItemAt(0);
        }
    }
}
