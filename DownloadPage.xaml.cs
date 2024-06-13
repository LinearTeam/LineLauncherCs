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
        public DownloadPage()
        {
            InitializeComponent();
            refreshContent();
        }
        public void refreshContent()
        {
            confirm.Content = MainWindow.i18NTools.getString(confirm.Content.ToString());
            rel.Content = MainWindow.i18NTools.getString(rel.Content.ToString());
            pre.Content = MainWindow.i18NTools.getString(pre.Content.ToString()); 
            old.Content = MainWindow.i18NTools.getString(old.Content.ToString());
            spe.Content = MainWindow.i18NTools.getString(spe.Content.ToString());
            if (MainWindow.i18NTools.getLangName().Equals("en_US"))
            {
                confirm.FontFamily = new System.Windows.Media.FontFamily("Microsoft Yi Baiti");
                rel.FontFamily = new System.Windows.Media.FontFamily("Microsoft Yi Baiti");
                pre.FontFamily = new System.Windows.Media.FontFamily("Microsoft Yi Baiti");
                old.FontFamily = new System.Windows.Media.FontFamily("Microsoft Yi Baiti");
                spe.FontFamily = new System.Windows.Media.FontFamily("Microsoft Yi Baiti");
                vlist.FontFamily = new System.Windows.Media.FontFamily("Microsoft Yi Baiti");
            }
        }
    }
}
