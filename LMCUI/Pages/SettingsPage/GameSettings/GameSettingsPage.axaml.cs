using Avalonia.Controls;

namespace LMCUI.Pages.SettingsPage.GameSettings;

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using FluentAvalonia.UI.Controls;
using I18n;
using LMC;
using LMC.Basic;
using LMC.Basic.Configs;
using LMC.Basic.Logging;
using LMCCore.Java;
using Utils;

public partial class GameSettingsPage : PageBase {
    private ObservableCollection<JavaItem> _javaItems = new();
    private readonly Logger _logger = new Logger("GameSettingsPage");
    
    public GameSettingsPage() : base(I18nManager.Instance.GetString("Pages.SettingsPage.GameSettingsPage.Title"), "GameSettingsPage") {
        InitializeComponent();
        Loaded += OnLoaded;
    }
    async void OnLoaded(object? sender, RoutedEventArgs e) {
        await LoadConfigs();
        SearchStatus.Text = "";
    }

    async Task LoadConfigs() {
        await Dispatcher.UIThread.InvokeAsync(async () =>
        {
            await RefreshJavaItems();
            AutoSelectJavaToggleSwitch.IsChecked = Current.Config.AutoSelectJava;
        });
    }

    async Task RefreshJavaItems()
    {
        await Dispatcher.UIThread.InvokeAsync(RefreshJavaItemsInternal);
    }
    async Task RefreshJavaItemsInternal()
    {
        try
        {
            var list = new List<JavaItem>();
            var javas = new List<string>(Current.Config.JavaPaths);
            foreach (var path in javas)
            {
                var lj = await JavaManager.GetJavaInfo(path);
                list.Add(new JavaItem()
                {
                    Path = lj.Path,
                    Header =
                        $"{(lj.IsJdk ? "JDK" : "JRE")}-{lj.Version} {lj.Implementor} {(Current.Config.SelectedJavaPath.Equals(path) ? $"({I18nManager.Instance.GetString("Pages.SettingsPage.GameSettingsPage.JavaRuntime.JavaListExpander.JavaListItem.Enabled")})" : "")}",
                    IsSelected = Current.Config.SelectedJavaPath.Equals(path)
                });
            }
            
            jle.Header = I18nManager.Instance.GetString(list.Count == 0 ? "Pages.SettingsPage.GameSettingsPage.JavaRuntime.JavaListExpander.EmptyHeader" : "Pages.SettingsPage.GameSettingsPage.JavaRuntime.JavaListExpander.Header");
            list.ForEach(ji => ji.Foreground = (ji.IsSelected ? Brushes.LawnGreen : Foreground)!);
            _javaItems = new ObservableCollection<JavaItem>(list);
            jle.ItemsSource = _javaItems;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Refreshing Java Items");
        }
    }
    void SelectJava_Click(object? sender, RoutedEventArgs e) {
        var button = (Button)sender;
        var java = button.Tag.ToString();
        _logger.Info($"用户选择Java: {java}");
        
        if (Current.Config.JavaPaths.Contains(java))
        {
            Current.Config.SelectedJavaPath = java;
            ConfigManager.Save("app", Current.Config);
            _logger.Info("选择成功");
            Task.Run(RefreshJavaItems);
            return;
        }
        _logger.Warn("选择失败 (1)");
        Task.Run(RefreshJavaItems);
    }
    void RemoveJava_Click(object? sender, RoutedEventArgs e) {
        var button = (Button)sender;
        var java = button.Tag.ToString();
        _logger.Info($"用户移除Java: {java}");
        
        if (Current.Config.JavaPaths.Contains(java))
        {
            JavaManager.RemoveJava(java);
            _logger.Info("移除成功");
            _ = Task.Run(async () => await RefreshJavaItems());
            return;
        }
        _logger.Warn("移除失败 (1)");
        _ = Task.Run(async () => await RefreshJavaItems());
        
    }
    async void SearchJava_Click(object? sender, RoutedEventArgs e) {
        var progress = new Action<TaskCallbackInfo>(info => {
            SearchStatus.Text = I18nManager.Instance.GetString("Pages.SettingsPage.GameSettingsPage.JavaRuntime.ImportExpander.StatusText.SearchProgress",
                info.Progress, info.Total, I18nManager.Instance.GetString(info.Message));
        });

        var button = (Button)sender!;
        button.IsEnabled = false;
        
        try {
            var javas = await JavaManager.SearchJava(progress);
            foreach (var java in javas) {
                await JavaManager.AddJava(java);
            }
            SearchStatus.Text = I18nManager.Instance.GetString("Pages.SettingsPage.GameSettingsPage.JavaRuntime.ImportExpander.StatusText.SearchSuccess");
        }
        catch (Exception ex) {
            _logger.Error(ex, "Searching Java");
            SearchStatus.Text = I18nManager.Instance.GetString("Pages.SettingsPage.GameSettingsPage.JavaRuntime.ImportExpander.StatusText.SearchFailed", ex.Message);
        }
        finally {
            await RefreshJavaItems();
            button.IsEnabled = true;
        }
    }
    void JavaItemExpander_Click(object? sender, RoutedEventArgs e) {
        var path = ((SettingsExpanderItem)sender!).Tag!.ToString();
        path = Path.GetFullPath(path);
        CrossPlatformUtils.OpenFolderInExplorer(path);
    }
    async void AddJava_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            var typeFilter = new FilePickerFileType(RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "java.exe" : "java");
            typeFilter.Patterns = [typeFilter.Name];
            var files = await CrossPlatformUtils.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                AllowMultiple = false,
                FileTypeFilter = [typeFilter],
                Title = I18nManager.Instance.GetString("Pages.SettingsPage.GameSettingsPage.JavaRuntime.ImportExpander.ChooseJavaDialog.Title")
            });
            var file = files.FirstOrDefault();
            if (file == null) { return; }
            var root = Path.GetDirectoryName(file.Path.LocalPath);
            if (root.EndsWith("bin"))
            {
                root = Path.GetDirectoryName(root);
            }

            await Task.Run(() => JavaManager.AddJava(root));

            SearchStatus.Text = I18nManager.Instance.GetString("Pages.SettingsPage.GameSettingsPage.JavaRuntime.ImportExpander.StatusText.AddSuccess");
        }
        catch (Exception ex)
        {
            SearchStatus.Text = I18nManager.Instance.GetString("Pages.SettingsPage.GameSettingsPage.JavaRuntime.ImportExpander.StatusText.AddFailed", ex.Message);
            _logger.Error(ex, "添加 Java");
        }
        await RefreshJavaItems();
    }
    async void AutoSelectJava_Changed(object? sender, RoutedEventArgs e)
    {
        var isChecked = AutoSelectJavaToggleSwitch.IsChecked ?? true;

        await Task.Run(() =>
        {
            Current.Config.AutoSelectJava = isChecked;
            ConfigManager.Save("app", Current.Config);
        });
    }
}

public record JavaItem {
    public string Path { get; set; } 
    public string Header { get; set; }
    public IBrush Foreground { get; set; }
    public bool IsSelected { get; set; }
}