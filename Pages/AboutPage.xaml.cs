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
using Page = iNKORE.UI.WPF.Modern.Controls.Page;

namespace LMC.Pages
{
    /// <summary>
    /// HomePage.xaml 的交互逻辑
    /// </summary>
    public partial class AboutPage : Page
    {
        public AboutPage()
        {
            InitializeComponent();
            SizeChanged += AboutPage_SizeChanged;
            double width = this.Width;
            this.Width = 800;
            AboutPage_SizeChanged(null, null);
            Task.Run(async () => {
                await Task.Delay(20);
                this.Width = width;
                AboutPage_SizeChanged(null, null);
            });
        }

        private void AboutPage_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            try
            {
                CD.Width = new GridLength(AboutExpander.ActualWidth - SSP.ActualWidth - 30);
            }catch { }
        
        }
    }
}
