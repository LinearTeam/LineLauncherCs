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
using LMC.Minecraft.Profile;
using LMC.Minecraft.Profile.Model;
using LMC.Utils;
using ListView = iNKORE.UI.WPF.Modern.Controls.ListView;
using Page = iNKORE.UI.WPF.Modern.Controls.Page;

namespace LMC.Pages.ProfilePage
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
        private List<LocalProfile> _localProfiles = new List<LocalProfile>();
        public static ProfilePage Instance;
        public ProfilePage()
        {
            Instance = this;
            InitializeComponent();
            this.Loaded += (a, b) =>
            {
                Dispatcher.InvokeAsync(RefreshUi);
            };
        }         

        public async Task RefreshUi()
        {
            Dispatcher.InvokeAsync(() => LoadingMask.Visibility = Visibility.Visible);
            await Dispatcher.InvokeAsync(async () =>
            {
                ir.ItemsSource = _profileItems;
                gr.ItemsSource = _gamePathItems;
                var gamePath = ProfileManager.GetSelectedGamePath();
                var profiles = await ProfileManager.GetProfiles(gamePath).ConfigureAwait(false);
                _localProfiles = profiles;
                _profileItems.Clear();
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
            });
            Dispatcher.InvokeAsync(() => LoadingMask.Visibility = Visibility.Collapsed);
        }

        private void ButtonBase_OnClick(object sender, RoutedEventArgs e)
        {
            MainWindow.Navigate(DownloadPage.DownloadPage.DownloadMinecraftPage);
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
                if (ProfileManager.IsSameGamePath(gp, ProfileManager.GetSelectedGamePath())) continue;
                _gamePathItems.Add(gamePathItem);
            }
            var sgp = ProfileManager.GetSelectedGamePath();
            GamePathItem gpi = new GamePathItem();
            gpi.Path = sgp.Path;
            gpi.Name = sgp.Name;
            _gamePathItems.Insert(0, gpi);
            gr.SelectedIndex = 0;
            fview.SelectedIndex = 0;
            scd.ShowAsync();
        }

        private void Confirm_Clicked(object sender, RoutedEventArgs e)
        {
            var gpi = _gamePathItems[gr.SelectedIndex];
            ProfileManager.SetSelectedGamePath(new GamePath(gpi));
            Dispatcher.InvokeAsync(RefreshUi);
            scd.Hide();
        }

        private void Cancel_Clicked(object sender, RoutedEventArgs e)
        {
            scd.Hide();
        }

        private void Delete_OnClick(object sender, RoutedEventArgs e)
        {
            var deletedItem = _gamePathItems[gr.SelectedIndex];
            if(ProfileManager.IsSameGamePath(new GamePath("1","./.minecraft"), new GamePath(deletedItem))) return;
            _gamePathItems.Remove(deletedItem);
            ProfileManager.DeleteGamePath(new GamePath(deletedItem));
        }

        private void Gr_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if(_gamePathItems.Count <= 0) return;
            if(((ListView) sender).SelectedIndex < 0) ((ListView)sender).SelectedIndex = 0;
            var deletedItem = _gamePathItems[gr.SelectedIndex];
            delete.IsEnabled = !ProfileManager.IsSameGamePath(new GamePath("1", "./.minecraft"), new GamePath(deletedItem));
        }

        private void Add_OnClick(object sender, RoutedEventArgs e)
        {
            fview.SelectedIndex = 1;
            
            string name = AddName.Text.Trim();
            string path = AddPath.Text;
            if(!Directory.Exists(path) || string.IsNullOrEmpty(name) || string.IsNullOrWhiteSpace(name) || name.Contains("|") || name.Length > 15)
            {
                AddConfirm.IsEnabled = false;
            } else AddConfirm.IsEnabled = true;
        }

        private void SelectPath_Click(object sender, RoutedEventArgs e)
        {
            string path = FolderPicker.SelectFolder(MainWindow.Instance, "请选择 .minecraft 文件夹");
            if (!string.IsNullOrEmpty(path))
            {
                AddPath.Text = path;
            }
        }

        private void Add_TextChange(object sender, TextChangedEventArgs e)
        {
            string name = AddName.Text.Trim();
            string path = AddPath.Text;
            if(!Directory.Exists(path) || string.IsNullOrEmpty(name) || string.IsNullOrWhiteSpace(name) || name.Contains("|") || name.Length > 15)
            {
                AddConfirm.IsEnabled = false;
            } else AddConfirm.IsEnabled = true;
        }

        private void AddCancel_OnClick(object sender, RoutedEventArgs e)
        {
            fview.SelectedIndex = 0;
        }

        private void AddConfirm_OnClick(object sender, RoutedEventArgs e)
        {
            string name = AddName.Text.Trim();
            string path = AddPath.Text;
            if(!Directory.Exists(path) || string.IsNullOrEmpty(name) || string.IsNullOrWhiteSpace(name) || name.Contains("|") || name.Length > 15)
            {
                AddConfirm.IsEnabled = false;
            } else AddConfirm.IsEnabled = true;
            if(!AddConfirm.IsEnabled) return;
            GamePath gp = new GamePath(AddName.Text.TrimStart().TrimEnd(), AddPath.Text);
            ProfileManager.AddGamePath(gp);
            fview.SelectedIndex = 0;
            scd.Hide();
            RefreshUi();
        }

        private void SC_Click(object sender, RoutedEventArgs e)
        {
            var sc = (SettingsCard)sender;
            foreach (var p in _localProfiles)
            {
                if (p.Name.Equals(sc.Header))
                {
                    MainWindow.Navigate(new ProfileManagePage(p));
                    return;
                }
            }
        }
    }
}