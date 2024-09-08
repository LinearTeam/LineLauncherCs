using LMC.Minecraft;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
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
        bool _isForge = false;
        bool _isOpt = false;
        bool _isFab = false;
        public DownloadCurrentVersionPage()
        {
            InitializeComponent();
            RefreshContent();
            verName_TextChanged(null, null);
        }
        public static string CurrentVersion = "1.21";
        private void back_Click(object sender, RoutedEventArgs e)
        {
            MainWindow.MainNagView.Navigate(typeof(DownloadPage));
        }

        async private void RefreshContent()
        {
            /*
            confirm.Content = MainWindow.I18NTools.getString(confirm.Content.ToString());
            dvTitle.Text = MainWindow.I18NTools.getString(dvTitle.Text);
            if (MainWindow.I18NTools.getLangName().ToLower().Equals("en_us"))
            {
                dvTitle.FontFamily = new System.Windows.Media.FontFamily("Microsoft Yi Baiti");
                confirm.FontFamily = new System.Windows.Media.FontFamily("Microsoft Yi Baiti");
                forge.FontFamily = new System.Windows.Media.FontFamily("Microsoft Yi Baiti");
                fab.FontFamily = new System.Windows.Media.FontFamily("Microsoft Yi Baiti");
                opt.FontFamily = new System.Windows.Media.FontFamily("Microsoft Yi Baiti");
            }
            */
            verName.Text = CurrentVersion;
            GameDownloader gameDownloader = new GameDownloader();
            gameDownloader.Bmclapi();
            var versionList = await gameDownloader.GetForgeFabricOptifineVersionList(CurrentVersion);
            forge.Items.Clear();
            fab.Items.Clear();
            opt.Items.Clear();
            string ff = MainWindow.I18NTools.GetString("lmc.pages.dcp.failed").Replace("{type}","Forge");
            string of = MainWindow.I18NTools.GetString("lmc.pages.dcp.failed").Replace("{type}", "Optifine");
            string bf = MainWindow.I18NTools.GetString("lmc.pages.dcp.failed").Replace("{type}", "Fabric");
            if (versionList.fabs.First().Equals("N"))
            {
                bf = MainWindow.I18NTools.GetString("lmc.pages.dcp.notsupp").Replace("{type}", "Fabric");
            }
            if (versionList.forges.First().Equals("N"))
            {
                ff = MainWindow.I18NTools.GetString("lmc.pages.dcp.notsupp").Replace("{type}", "Forge");
            }
            if (versionList.opts.First().Equals("N"))
            {
                of = MainWindow.I18NTools.GetString("lmc.pages.dcp.notsupp").Replace("{type}", "Optifine");
            }
            foreach ( string f in versionList.forges ) 
            {
                if (f.Equals("N")) continue;
                forge.Items.Add(f);
                ff = MainWindow.I18NTools.GetString("lmc.pages.dcp.plzc").Replace("{type}", "Forge");
            }
            foreach (string f in versionList.fabs)
            {
                if (f.Equals("N")) continue;
                fab.Items.Add(f);
                bf = MainWindow.I18NTools.GetString("lmc.pages.dcp.plzc").Replace("{type}", "Fabric");
            }
            foreach (string f in versionList.opts)
            {
                if (f.Equals("N")) continue;
                opt.Items.Add(f);
                of = MainWindow.I18NTools.GetString("lmc.pages.dcp.plzc").Replace("{type}", "Optifine");
            }
            forge.Items.Insert(0,ff);
            fab.Items.Insert(0,bf);
            opt.Items.Insert(0, of);
            forge.SelectedItem = forge.Items.GetItemAt(0);
            fab.SelectedItem = fab.Items.GetItemAt(0);
            opt.SelectedItem = opt.Items.GetItemAt(0);
        }
        async private Task OptTip()
        {
            await MainWindow.ShowMsgBox("提示", "您正在同时使用Fabric和Optifine，启动器将为您自动安装Optifabric模组以让Fabric兼容Optifine。\n请注意，使用Optifine将导致严重的模组问题，若游戏界面发生任何编码错误等显示问题或模组兼容问题，请先尝试卸载Optifine和Optifabric。", "");
        }
        async private void opt_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            _isOpt = opt.SelectedIndex != 0;
            if (_isOpt && _isFab) await OptTip();
        }
        
        async private void fab_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            _isFab = fab.SelectedIndex != 0;
            if (_isOpt && _isFab) await OptTip();
            if (_isFab) forge.SelectedIndex = 0;
        }

        private void forge_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            _isForge = forge.SelectedIndex != 0;
            if(_isForge) fab.SelectedIndex = 0;

        }

        private bool CheckVersionName(string name)
        {
            if (Regex.IsMatch(name, @"^\s|\s$"))
            {
                return false;
            }
            if (Regex.IsMatch(name, @"[\\/:\?\*\|\""]"))
            {
                return false;
            }
            if (name.Length > 100)
            {
                return false;
            }
            if (name.Contains("\n") || name.Contains("\r"))
            {
                return false;
            }

            foreach (char c in name)
            {
                if (c < 0x20 || c == 0x7F)
                {
                    return false;
                }

                if (char.IsControl(c))
                {
                    return false;
                }
            }
            if (Directory.Exists($"{GameDownloader.GamePath}/versions/{name}"))
            {
                return false;
            }

            return true;
        }

        private void verName_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (string.IsNullOrEmpty(verName.Text))
            {
                validMsg.Content = "请输入版本名！";
                validMsg.Foreground = new SolidColorBrush(Color.FromScRgb(100,1,1,0));
                return;
            }
            if(CheckVersionName(verName.Text))
            {
                validMsg.Content = "版本名称可用！";
                validMsg.Foreground = new SolidColorBrush(Color.FromScRgb(100, 0, 1, 0));
                return;
            }
            validMsg.Content = "版本名不可用，可能因为文件夹已存在，以空格开头、结尾，包含特殊字符（如\\、/、|、\"、:、?等）";
            validMsg.Foreground = new SolidColorBrush(Color.FromScRgb(100, 1, 0, 0));
        }
    }
}
