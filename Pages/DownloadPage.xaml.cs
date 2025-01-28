using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using iNKORE.UI.WPF.Controls;
using iNKORE.UI.WPF.Modern.Controls;
using LMC.Minecraft;
using Page = iNKORE.UI.WPF.Modern.Controls.Page;

namespace LMC.Pages
{
    /// <summary>
    ///     HomePage.xaml 的交互逻辑
    /// </summary>
    public partial class DownloadPage : Page
    {
        public static (List<DVersion> normal, List<DVersion> alpha, List<DVersion> beta) Versions;
/*
        static DownloadPage()
        {
            Task.Run(async () =>
            {
                GameDownloader gd = new GameDownloader();
                Versions = gd.ParseManifest(await gd.GetVersionManifest());
            });
        }*/
        
        public DownloadPage()
        {
            Loaded += OnLoaded;
            InitializeComponent();
        }

        private async void OnLoaded(object sender, RoutedEventArgs e)
        {
            RefreshVersionList();
        }

        private async Task RefreshVersionList()
        {
            bool isOld = true;
            bool isPre = true;
            bool isRel = true;
            var res = new List<DVersion>();
            var gd = new GameDownloader();
            if (Versions.normal == null || Versions.beta == null || Versions.alpha == null) Versions = gd.ParseManifest(await gd.GetVersionManifest());
            foreach (var ver in Versions.normal)
                if (isRel && ver.Type == 0)
                {
                    res.Add(ver);
                }
                else if (isPre && ver.Type == 1)
                {
                    res.Add(ver);
                }

            if (isOld)
            {
                foreach (var ver in Versions.beta) res.Add(ver);
                foreach (var ver in Versions.alpha) res.Add(ver);
            }

            foreach (var ver in res) ls.Items.Add(ver.Id);
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
        
        private void Ls_OnSelected(object sender, RoutedEventArgs e)
        {
            if (ls.SelectedItems.Count >= 1)
            {
                if (ls.SelectedItems.Count > 0)
                {
                    MainWindow.Navigate(MainWindow.DownloadPage);
                    var gd = new GameDownloader();
                    ContentDialog cd = new ContentDialog();
                    cd.CloseButtonText = "取消";
                    cd.PrimaryButtonText = "确定";
                    cd.DefaultButton = ContentDialogButton.Primary;
                    SimpleStackPanel sp = new SimpleStackPanel();
                    TextBox tb = new TextBox();
                    sp.Children.Add(tb);
                    cd.Content = sp;
                    Label l = new Label();
                    sp.Children.Add(l);
                    l.Content = "请输入版本名";
                    bool val = false;
                    tb.TextChanged += (s, e) =>
                    {

                        if (string.IsNullOrEmpty(tb.Text))
                        {
                            l.Content = "请输入版本名！";
                            l.Foreground = new SolidColorBrush(Color.FromScRgb(100,1,1,0));
                            val = false;
                            return;
                        }
                        if(CheckVersionName(tb.Text))
                        {
                            l.Content = "版本名称可用！";
                            l.Foreground = new SolidColorBrush(Color.FromScRgb(100, 0, 1, 0));
                            val = true;
                            return;
                        }
                        l.Content = "版本名不可用，可能因为文件夹已存在，以空格开头、结尾，包含特殊字符（如\\、/、|、\"、:、?等）";
                        l.Foreground = new SolidColorBrush(Color.FromScRgb(100, 1, 0, 0));
                        val = false;
                    };
                    cd.PrimaryButtonClick += (dialog, args) =>
                    {
                        if(!val) return;
                        gd.DownloadGame(ls.SelectedItems[0] as string, tb.Text, false, false, false);
                        cd.Hide();
                    };
                    cd.ShowAsync();
                }
            }
        }
    }
}