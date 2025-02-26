using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using iNKORE.UI.WPF.Modern.Controls;
using LMC.Basic;
using LMC.Minecraft;
using Page = iNKORE.UI.WPF.Modern.Controls.Page;

namespace LMC.Pages
{
    /// <summary>
    ///     HomePage.xaml 的交互逻辑
    /// </summary>
    public partial class ProfileManagePage : Page
    {
        private static readonly Logger s_logger = new Logger("PMP");
        private readonly LocalProfile _profile;

        public ProfileManagePage(LocalProfile profile)
        {
            _profile = profile;
            InitializeComponent();
            Loaded += OnLoaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            HeaderCard.Header = _profile.Name;
            var image = new BitmapImage();
            var uc = false;
            try
            {
                using (var fs = new FileStream(_profile.IconPath, FileMode.Open, FileAccess.Read))
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
                if (_profile.IconPath != null)
                {
                    s_logger.Warn("加载版本图标失败：" + ex.Message);
                    MainWindow.Instance.EnqueueMessage(new InfoBarMessage($"加载版本 {_profile.Name} 图标失败：{ex.Message}",
                        InfoBarSeverity.Error, "档案"));
                }

                uc = false;
            }

            var imageKey = "Vanilla";
            if (_profile.Status != ProfileStatus.Normal)
            {
                switch (_profile.Status)
                {
                    case ProfileStatus.Unknown:
                        HeaderCard.Description = "错误：无法识别版本号";
                        imageKey = "Unknown";
                        break;
                    case ProfileStatus.NoJar:
                        HeaderCard.Description = "错误：没有找到版本 Jar";
                        imageKey = "Error";
                        break;
                    case ProfileStatus.NoJson:
                        HeaderCard.Description = "错误：没有找到版本索引文件";
                        imageKey = "Error";
                        break;
                    case ProfileStatus.FailedToReadJson:
                        HeaderCard.Description = "错误：版本索引文件出错";
                        imageKey = "Error";
                        break;
                    case ProfileStatus.UnknownLoader:
                        HeaderCard.Description = $"{_profile.Version} 未知加载器 (将以原版启动)";
                        imageKey = "UnSupport";
                        break;
                }
            }
            else if (_profile.ModLoader.ModLoaderType == ModLoaderType.Vanilla)
            {
                HeaderCard.Description = "原版 " + _profile.Version;
                imageKey = "Vanilla";
            }
            else
            {
                switch (_profile.ModLoader.ModLoaderType)
                {
                    case ModLoaderType.Fabric:
                        HeaderCard.Description = $"Fabric {_profile.Version} {_profile.ModLoader.LoaderVersion}";
                        imageKey = "Fabric";
                        break;
                    case ModLoaderType.Forge:
                        HeaderCard.Description = $"Fabric {_profile.Version} {_profile.ModLoader.LoaderVersion}";
                        imageKey = "Forge";
                        break;
                    case ModLoaderType.Other:
                        HeaderCard.Description = $"{_profile.Version} 其他加载器 (如Quilt等，无法正常启动，请等待更新)";
                        imageKey = "UnSupport";
                        break;
                    case ModLoaderType.NeoForge:
                        HeaderCard.Description = $"NeoForge {_profile.Version} {_profile.ModLoader.LoaderVersion}";
                        imageKey = "NeoForge";
                        break;
                }
            }

            if (_profile.ModLoader != null && _profile.ModLoader.ModLoaderType != ModLoaderType.NeoForge &&
                _profile.ModLoader.ModLoaderType != ModLoaderType.Forge &&
                _profile.ModLoader.ModLoaderType != ModLoaderType.Fabric)
                ModExpander.IsEnabled = false;

            if (_profile.Status != ProfileStatus.Normal) SetExpander.IsEnabled = false;

            var source = uc ? image : ((Image)grid.FindResource(imageKey)).Source;
            HeaderCard.HeaderIcon = new Image { Source = source };
        }

        private async void Delete(object sender, RoutedEventArgs e)
        {
            try
            {
                var res = await MainWindow.ShowDialog("取消", "这将删除该版本的存档、模组、设置等内容，以及存放在版本文件夹下的数据，是否继续？", "删除",
                    ContentDialogButton.Close, "确认");
                MainWindow.Navigate(MainWindow.ProfilePage);
                if(res == ContentDialogResult.Primary) await ProfileManager.DeleteProfile(_profile);
                await Task.Delay(300);
                ProfilePage.Instance.RefreshUi();
            }
            catch(Exception ex)
            {
                s_logger.Error("删除版本时失败: " + ex.Message + "\n" + ex.StackTrace);
                MainWindow.Instance.EnqueueMessage(new InfoBarMessage("删除版本时失败: " + ex.Message, InfoBarSeverity.Error, "错误"));
            }
        }

        private void Choose(object sender, RoutedEventArgs e)
        {
            ProfileManager.ChooseProfile(_profile);
            MainWindow.Instance.EnqueueMessage(new InfoBarMessage("", InfoBarSeverity.Informational, "已选择"));
        }
    }
}