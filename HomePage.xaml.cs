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
            refreshContent();
        }
        public void refreshContent()
        {
            i18nTools i18n = new i18nTools();
            noticeTitle.Text = i18n.getString(noticeTitle.Text);
            launchButton.Content = i18n.getString(launchButton.Content.ToString());
        }
    }
}
