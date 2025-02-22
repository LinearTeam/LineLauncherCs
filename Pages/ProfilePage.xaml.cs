using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using iNKORE.UI.WPF.Modern.Controls;
using LMC.Basic;
using LMC.Minecraft;
using Page = iNKORE.UI.WPF.Modern.Controls.Page;

namespace LMC.Pages
{
    public class ProfileItem
    {
        public ImageSource Image { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
    }

    public class GamePathItem
    {
        public string Name { get; set; }
        public string Path { get; set; }
    }

    /// <summary>
    ///     HomePage.xaml 的交互逻辑
    /// </summary>
    public partial class ProfilePage : Page
    {
        private static readonly Logger s_logger = new Logger("PG");
        private readonly ObservableCollection<ProfileItem> _profileItems = new ObservableCollection<ProfileItem>();
        private readonly ObservableCollection<GamePathItem> _gamePathItems = new ObservableCollection<GamePathItem>();
        private bool _initialized = false;
        public static ProfilePage Instance;
        public ProfilePage()
        {
            Instance = this;
            InitializeComponent();
            this.Loaded += (a, b) =>
            {
                if (!_initialized) RefreshUi();
            };
        }         

        public async Task RefreshUi()
        {
            _initialized = true;
            _profileItems.Clear();
            ir.ItemsSource = _profileItems;
            gr.ItemsSource = _gamePathItems;
            var gamePath = ProfileManager.GetSelectedGamePath();
            var profiles = await ProfileManager.GetProfiles(gamePath);
            var profileItems = new List<ProfileItem>();
            foreach (var profile in profiles)
            {
                var profileItem = new ProfileItem();
                profileItem.Name = profile.Name;
                var image = new BitmapImage();
                bool uc = false;
                try
                {
                    using (var fs = new FileStream(profile.IconPath, FileMode.Open, FileAccess.Read))
                    {
                        image.BeginInit();
                        image.CacheOption = BitmapCacheOption.OnLoad;
                        image.StreamSource = fs;
                        image.EndInit();
                    }

                    uc = true;
                }
                catch (Exception ex)
                {
                    if (profile.IconPath != null)
                    {
                        s_logger.Warn("加载版本图标失败：" + ex.Message);
                        MainWindow.Instance.EnqueueMessage(new InfoBarMessage($"加载版本 {profile.Name} 图标失败：{ex.Message}", InfoBarSeverity.Error, "档案"));
                    }
                    uc = false;
                }

                string ImageKey = "Vanilla";
                if (profile.Status != ProfileStatus.Normal)
                {
                    switch (profile.Status)
                    {
                        case ProfileStatus.Unknown:
                            profileItem.Description = "错误：无法识别版本号";
                            ImageKey = "Unknown";
                            break;
                        case ProfileStatus.NoJar:
                            profileItem.Description = "错误：没有找到版本 Jar";
                            ImageKey = "Error";
                            break;
                        case ProfileStatus.NoJson:
                            profileItem.Description = "错误：没有找到版本索引文件";
                            ImageKey = "Error";
                            break;
                        case ProfileStatus.FailedToReadJson:
                            profileItem.Description = "错误：版本索引文件出错";
                            ImageKey = "Error";
                            break;
                        case ProfileStatus.UnknownLoader:
                            profileItem.Description = $"{profile.Version} 未知加载器 (将以原版启动)";
                            ImageKey = "UnSupport";
                            break;
                    }
                }
                else if (profile.ModLoader.ModLoaderType == ModLoaderType.Vanilla)
                {
                    profileItem.Description = "原版 " + profile.Version;
                    ImageKey = "Vanilla";
                }
                else
                {
                    switch (profile.ModLoader.ModLoaderType)
                    {
                        case ModLoaderType.Fabric:
                            profileItem.Description = $"Fabric {profile.Version} {profile.ModLoader.LoaderVersion}";
                            ImageKey = "Fabric";
                            break;
                        case ModLoaderType.Forge:
                            profileItem.Description = $"Fabric {profile.Version} {profile.ModLoader.LoaderVersion}";
                            ImageKey = "Forge";
                            break;
                        case ModLoaderType.Other:
                            profileItem.Description = $"{profile.Version} 其他加载器 (如Quilt等，无法正常启动，请等待更新)";
                            ImageKey = "UnSupport";
                            break;
                        case ModLoaderType.NeoForge:
                            profileItem.Description = $"NeoForge {profile.Version} {profile.ModLoader.LoaderVersion}";
                            ImageKey = "NeoForge";
                            break;
                    }
                }
                profileItem.Image = uc ? image : ((Image)grid.FindResource(ImageKey)).Source;
                _profileItems.Add(profileItem);
            }
        }

        private void ButtonBase_OnClick(object sender, RoutedEventArgs e)
        {
            MainWindow.Navigate(DownloadPage.DownloadMinecraftPage);
        }

        private void SwitchGamePath(object sender, RoutedEventArgs e)
        {
            _gamePathItems.Clear();
            var gps = ProfileManager.GetGamePaths();
            foreach (var gp in gps)
            {
                GamePathItem gamePathItem = new GamePathItem();
                gamePathItem.Path = gp.Path;
                gamePathItem.Name = gp.Name;
                if (Path.GetFullPath(gp.Path).Equals(ProfileManager.GetSelectedGamePath().Path)) continue;
                _gamePathItems.Add(gamePathItem);
            }
            var sgp = ProfileManager.GetSelectedGamePath();
            GamePathItem gpi = new GamePathItem();
            gpi.Path = sgp.Path;
            gpi.Name = sgp.Name;
            _gamePathItems.Insert(0, gpi);
            gr.SelectedIndex = 0;
            scd.ShowAsync();
        }
    }
}