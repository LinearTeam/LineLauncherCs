using LMC.Basic;
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
    /// HomePage.xaml 的交互逻辑
    /// </summary>
    public partial class HomePage : Page
    {
        public HomePage()
        {
            InitializeComponent();
//            refreshContent();
        }
        /*
        public void refreshContent()
        {
            noticeTitle.Text = MainWindow.I18NTools.getString(noticeTitle.Text);
            launchButton.Content = MainWindow.I18NTools.getString(launchButton.Content.ToString());
            if (MainWindow.I18NTools.getLangName().Equals("en_US"))
            {
                noticeTitle.FontFamily = new System.Windows.Media.FontFamily("Microsoft Yi Baiti");
                launchButton.FontFamily = new System.Windows.Media.FontFamily("Microsoft Yi Baiti");
                launchInfo.FontFamily = new System.Windows.Media.FontFamily("Microsoft Yi Baiti");
                notice.FontFamily = new System.Windows.Media.FontFamily("Microsoft Yi Baiti");
            }
        }
        */
    }
}
