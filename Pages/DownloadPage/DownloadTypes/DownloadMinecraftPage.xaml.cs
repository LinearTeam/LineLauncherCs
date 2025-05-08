using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using iNKORE.UI.WPF.Modern;
using iNKORE.UI.WPF.Modern.Common.IconKeys;
using iNKORE.UI.WPF.Modern.Controls;
using LMC.Basic;
using LMC.Basic.Config;
using LMC.Minecraft;
using LMC.Minecraft.Download;
using LMC.Minecraft.Download.Model;
using LMC.Minecraft.Profile;
using Page = iNKORE.UI.WPF.Modern.Controls.Page;

namespace LMC.Pages.DownloadPage.DownloadTypes
{
    /// <summary>
    ///     HomePage.xaml 的交互逻辑
    /// </summary>
    public class MinecraftItem
    {
        public string Id { get; set; }
        public string Time { get; set; }
        public FontIconData Icon { get; set; }
    }

    public partial class DownloadMinecraftPage : Page
    {
        public static (List<DVersion> normal, List<DVersion> alpha, List<DVersion> beta) Versions;
        private readonly object _lock = new object();
        private readonly List<DVersion> allVersions = new List<DVersion>();
        private readonly ObservableCollection<MinecraftItem> displayedVersions = new ObservableCollection<MinecraftItem>();
        private bool _val;

        public DownloadMinecraftPage()
        {
            InitializeComponent();
            DataContext = this;
            Loaded += OnLoaded;
        }

        private async void OnLoaded(object sender, RoutedEventArgs e)
        {
            bool light = !string.IsNullOrEmpty(Config.ReadGlobal("ui", "theme"))
                ? Config.ReadGlobal("ui", "theme") == "light"
                : ThemeManager.Current.ApplicationTheme == ApplicationTheme.Light;
            LoadingMask.Background = light
                ? new SolidColorBrush(Colors.White)
                : new SolidColorBrush(Color.FromRgb(35, 39, 52));
            fview.SelectedIndex = 0;
            back.IsEnabled = false;
            next.IsEnabled = false;
            Task.Run(LoadDataAsync);
        }

        private async Task LoadDataAsync()
        {
            Dispatcher.Invoke(() => LoadingMask.Visibility = Visibility.Visible);
            try
            {
                await Task.Run(() => RefreshVersionList());
            }
            catch (Exception e)
            {
                new Logger("DMP").Error("获取 Minecraft 版本列表时出错：" + e.Message + "\n" + e.StackTrace + "\n" + e.InnerException);
                MainWindow.ShowDialog("确认",
                    "获取 Minecraft 版本列表时出错：" + e.Message + "\n" + e.StackTrace + "\n" + e.InnerException, "错误");
            }
            Dispatcher.Invoke(() => LoadingMask.Visibility = Visibility.Collapsed);
        }

        private void RefreshVersionList()
        {
            if (displayedVersions.Count > 10) return;
            bool isOld = true;
            bool isPre = true;
            bool isRel = true;

            var gd = new GameDownloader();
            var versions = Versions.normal == null || Versions.beta == null || Versions.alpha == null
                ? gd.ParseManifest(gd.GetVersionManifest().Result)
                : Versions;

            var res = new List<DVersion>();
            foreach (var ver in versions.normal)
                if (isRel && ver.Type == 0)
                {
                    res.Add(ver);
                }
                else if (isPre && ver.Type == 1) res.Add(ver);

            if (isOld)
            {
                res.AddRange(versions.beta);
                res.AddRange(versions.alpha);
            }

            lock (_lock)
            {
                allVersions.AddRange(res);
            }

            Dispatcher.Invoke(() =>
            {
                displayedVersions.Clear();
                foreach (var version in allVersions.Take(40)) // Load only the first 50 initially
                {
                    var item = new MinecraftItem();
                    item.Id = version.Id;

                    var dto = DateTimeOffset.Parse(version.ReleaseTime);
                    var utc8Time = dto.ToOffset(TimeSpan.FromHours(8)).DateTime;
                    string formattedTime = utc8Time.ToString("yyyy-MM-dd HH:mm:ss");
                    item.Time = formattedTime;
                    if (version.Type == 0)
                    {
                        item.Icon = SegoeFluentIcons.OEM;
                    }
                    else if (version.Type == 1)
                    {
                        item.Icon = SegoeFluentIcons.Bug;
                    }
                    else if (version.Type > 1) item.Icon = SegoeFluentIcons.History;

                    displayedVersions.Add(item);
                }

                lb.ItemsSource = displayedVersions;
            });
        }

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            string searchText = SearchBox.Text.ToLower();
            Task.Run(() =>
            {
                var filtered = allVersions.Where(v => v.Id.ToLower().Contains(searchText)).Take(40).ToList();
                Dispatcher.Invoke(() =>
                {
                    displayedVersions.Clear();
                    foreach (var version in filtered)
                    {
                        var item = new MinecraftItem();
                        item.Id = version.Id;
                        var dto = DateTimeOffset.Parse(version.ReleaseTime);
                        var utc8Time = dto.ToOffset(TimeSpan.FromHours(8)).DateTime;
                        string formattedTime = utc8Time.ToString("yyyy-MM-dd HH:mm:ss");
                        item.Time = formattedTime;
                        if (version.Type == 0)
                        {
                            item.Icon = SegoeFluentIcons.OEM;
                        }
                        else if (version.Type == 1)
                        {
                            item.Icon = SegoeFluentIcons.Bug;
                        }
                        else if (version.Type > 1) item.Icon = SegoeFluentIcons.History;

                        displayedVersions.Add(item);
                    }
                });
            });
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

            if (name.Contains("\n") || name.Contains("\r") || name.Contains("*"))
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

            if (Directory.Exists($"{ProfileManager.GetSelectedGamePath().Path}/versions/{name}"))
            {
                return false;
            }

            return true;
        }

        private async void ButtonBase_OnClick(object sender, RoutedEventArgs e)
        {
            try
            {
                if (sender is SettingsCard card)
                {
                    tbc.Text = card.Header.ToString();
                    vcard.Header = card.Header.ToString();
                    var gd = new GameDownloader();
                    LoadingMask.Visibility = Visibility.Visible;
                    var res = await gd.GetForgeFabricOptifineVersionList(card.Header.ToString());
                    fview.SelectedIndex = 1;
                    fab.Items.Clear();
                    opti.Items.Clear();
                    forge.Items.Clear();
                    fab.Items.Add("不安装");
                    opti.Items.Add("不安装");
                    forge.Items.Add("不安装");
                    fab.IsEnabled = true;
                    forge.IsEnabled = true;
                    opti.IsEnabled = true;
                    if (res.fabs == null || res.fabs.Count <= 0)
                    {
                        fab.IsEnabled = false;
                    }
                    else
                    {
                        res.fabs.ForEach(s => fab.Items.Add(s));
                    }

                    if (res.forges == null || res.forges.Count <= 0)
                    {
                        forge.IsEnabled = false;
                    }
                    else
                    {
                        res.forges.ForEach(s => forge.Items.Add(s));
                    }

                    if (res.opts == null || res.opts.Count <= 0)
                    {
                        opti.IsEnabled = false;
                    }
                    else
                    {
                        res.opts.ForEach(s => opti.Items.Add(s));
                    }

                    vcard.HeaderIcon = card.HeaderIcon;
                    fab.SelectedIndex = 0;
                    forge.SelectedIndex = 0;
                    opti.SelectedIndex = 0;
                    next.IsEnabled = true;
                    back.IsEnabled = true;
                    LoadingMask.Visibility = Visibility.Collapsed;
                }
            }
            catch(Exception ex)
            {
                new Logger("DMP").Error("加载指定版本的加载器信息时出错：" + ex.Message + "\n" + ex.StackTrace + "\n" + ex.InnerException);
                MainWindow.ShowDialog("确认",
                    "加载指定版本的加载器信息时出错：" + ex.Message + "\n" + ex.StackTrace + "\n" + ex.InnerException, "错误");
                fview.SelectedIndex = 0;
            }
        }
        
        private void TextBoxBase_OnTextChanged(object sender, TextChangedEventArgs e)
        {
            if (string.IsNullOrEmpty(tbc.Text))
            {
                lac.Content = "请输入版本名！";
                lac.Foreground = new SolidColorBrush(Colors.Gray);
                _val = false;
                return;
            }

            if (CheckVersionName(tbc.Text))
            {
                lac.Content = "版本名称可用！";
                lac.Foreground = new SolidColorBrush(Color.FromScRgb(100, 0, 1, 0));
                _val = true;
                return;
            }

            lac.Content = "版本名不可用，可能因为文件夹已存在，以空格开头、结尾，包含特殊字符（如\\、/、|、\"、:、?等）";
            lac.Foreground = new SolidColorBrush(Color.FromScRgb(100, 1, 0, 0));
            _val = false;

        }

        private void ButtonBase_OnClick_1(object sender, RoutedEventArgs e)
        {
            MainWindow.Navigate(MainWindow.LaunchPage);
        }

        private void ButtonBase_OnClick_2(object sender, RoutedEventArgs e)
        {
            fview.SelectedIndex = 0;
        }

        private void ButtonBase_OnClick_3(object sender, RoutedEventArgs e)
        {
            if (!_val) return;
            var gd = new GameDownloader();
            gd.DownloadGame(vcard.Header as string, tbc.Text, opti.SelectedIndex != 0, fab.SelectedIndex != 0, forge.SelectedIndex != 0, opti.SelectedItem as string,
                fab.SelectedItem as string, forge.SelectedItem as string);
            MainWindow.MainNagView.SelectedItem = MainWindow.MainNagView.MenuItems[3];
        }

        private async void Fview_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (fview.SelectedIndex == 0)
            {
                if (next != null && back != null)
                {
                    next.IsEnabled = false;
                    back.IsEnabled = false;
                    LoadingMask.Visibility = Visibility.Visible;
                    SearchBox_TextChanged(this, null);
                    LoadingMask.Visibility = Visibility.Collapsed;
                }
            }

        }
    }
}