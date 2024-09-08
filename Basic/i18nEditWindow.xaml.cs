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
using System.Windows.Shapes;

namespace LMC.Basic
{
    /// <summary>
    /// i18nEditWindow.xaml 的交互逻辑
    /// </summary>
    public partial class i18nEditWindow : Window
    {

        private I18nTools _i18NTools = new I18nTools();
        public i18nEditWindow()
        {
            InitializeComponent();
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                value.Text = (_i18NTools.GetString(key.Text, int.Parse(lang.Text)));
            }
            catch{ }
           
        }

        private void Button_Click_1(object sender, RoutedEventArgs e)
        {
            try
            {
                _i18NTools.SetString(key.Text,value.Text,int.Parse(lang.Text));
            }
            catch { }
        }
    }
}
