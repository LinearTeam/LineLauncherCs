using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
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

        private void Ls_OnSelected(object sender, RoutedEventArgs e)
        {
            if (ls.SelectedItems.Count >= 1)
            {
                if (ls.SelectedItems.Count > 0)
                {
                    MainWindow.Navigate(MainWindow.DownloadPage);
                    var gd = new GameDownloader();
                    gd.DownloadGame(ls.SelectedItems[0] as string, ls.SelectedItems[0] as string, false, false, false);
                }
            }
        }
    }
}