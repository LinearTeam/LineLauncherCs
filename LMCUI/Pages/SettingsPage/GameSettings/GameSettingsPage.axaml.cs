using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

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
    private Logger s_logger = new Logger("GameSettingsPage");
    
    public GameSettingsPage() : base(I18nManager.Instance.GetString("Pages.SettingsPage.GameSettingsPage.Title"), "GameSettingsPage") {
        InitializeComponent();
        Loaded += ((sender, args) => {
            _ = Task.Run(async () => await OnLoaded(sender, args));
        });
    }
    async Task OnLoaded(object? sender, RoutedEventArgs e) {
        await LoadConfigs();
        SearchStatus.Text = "";
    }

    async Task LoadConfigs() {
        await RefreshJavaItems();
        Dispatcher.UIThread.Invoke(() => { AutoSelectJavaToggleSwitch.IsChecked = Current.Config.AutoSelectJava; });
    }
    
    async Task RefreshJavaItems() {
        var list = new List<JavaItem>();
        var javas = new List<string>(Current.Config.JavaPaths);
        foreach (var path in javas)
        {
            var lj = await JavaManager.GetJavaInfo(path);
            list.Add(new JavaItem()
            {
                Path = lj.Path,
                Header = $"{(lj.IsJdk ? "JDK" : "JRE")}-{lj.Version} {lj.Implementor} {(Current.Config.SelectedJavaPath.Equals(path) ? $"({I18nManager.Instance.GetString("Pages.SettingsPage.GameSettingsPage.JavaRuntime.JavaListExpander.JavaListItem.Enabled")})" : "")}",
                IsSelected = Current.Config.SelectedJavaPath.Equals(path)
            });
        }
        Dispatcher.UIThread.Invoke(() => {
            if (list.Count == 0)
            {
                jle.Header = I18nManager.Instance.GetString("Pages.SettingsPage.GameSettingsPage.JavaRuntime.JavaListExpander.EmptyHeader");
            }
            else
            {
                jle.Header = I18nManager.Instance.GetString("Pages.SettingsPage.GameSettingsPage.JavaRuntime.JavaListExpander.Header");
            } 
            list.ForEach(ji => ji.Foreground = ji.IsSelected ? Brushes.LawnGreen : Foreground);
            _javaItems = new ObservableCollection<JavaItem>(list);
            return jle.ItemsSource = _javaItems;
        });
    }
    void SelectJava_Click(object? sender, RoutedEventArgs e) {
        var button = (Button)sender;
        var java = button.Tag.ToString();
        s_logger.Info($"用户选择Java: {java}");
        
        if (Current.Config.JavaPaths.Contains(java))
        {
            Current.Config.SelectedJavaPath = java;
            ConfigManager.Save("app", Current.Config);
            s_logger.Info("选择成功");
            _ = Task.Run(async () => await RefreshJavaItems());
            return;
        }
        s_logger.Warn("选择失败 (1)");
        _ = Task.Run(async () => await RefreshJavaItems());
    }
    void RemoveJava_Click(object? sender, RoutedEventArgs e) {
        var button = (Button)sender;
        var java = button.Tag.ToString();
        s_logger.Info($"用户移除Java: {java}");
        
        if (Current.Config.JavaPaths.Contains(java))
        {
            JavaManager.RemoveJava(java);
            s_logger.Info("移除成功");
            _ = Task.Run(async () => await RefreshJavaItems());
            return;
        }
        s_logger.Warn("移除失败 (1)");
        _ = Task.Run(async () => await RefreshJavaItems());
        
    }
    async void SearchJava_Click(object? sender, RoutedEventArgs e) {
        var progress = new Action<TaskCallbackInfo>((info => {
            Dispatcher.UIThread.Invoke(() =>
            SearchStatus.Text = I18nManager.Instance.GetString("Pages.SettingsPage.GameSettingsPage.JavaRuntime.ImportExpander.StatusText.SearchProgress",
                info.Progress, info.Total, I18nManager.Instance.GetString(info.Message)));
        }));

        var button = ((Button)sender);
        button.IsEnabled = false;
        
        var searchTask = Task.Run(() => JavaManager.SearchJava(progress));
      
        try {
            var javas = await searchTask;
            foreach (var java in javas) {
                await JavaManager.AddJava(java);
            }
            SearchStatus.Text = I18nManager.Instance.GetString("Pages.SettingsPage.GameSettingsPage.JavaRuntime.ImportExpander.StatusText.SearchSuccess");
        }
        catch (Exception ex) {
            s_logger.Error(ex, "Searching Java");
            SearchStatus.Text = I18nManager.Instance.GetString("Pages.SettingsPage.GameSettingsPage.JavaRuntime.ImportExpander.StatusText.SearchFailed", ex.Message);
        }
        finally {
            await RefreshJavaItems();
            button.IsEnabled = true;
        }
    }
    void JavaItemExpander_Click(object? sender, RoutedEventArgs e) {
        var path = ((SettingsExpanderItem)sender).Tag.ToString();
        path = Path.GetFullPath(path);
        CrossPlatformUtils.OpenFolderInExplorer(path);
    }
    void AddJava_Click(object? sender, RoutedEventArgs e) {
        _ = Task.Run(async () => {
            try
            {
                var typeFilter = new FilePickerFileType(RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "java.exe" : "java");
                typeFilter.Patterns = [typeFilter.Name];
                var files = await CrossPlatformUtils.OpenFilePickerAsync(new FilePickerOpenOptions()
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
                await JavaManager.AddJava(root);
                Dispatcher.UIThread.Invoke(() => SearchStatus.Text = I18nManager.Instance.GetString("Pages.SettingsPage.GameSettingsPage.JavaRuntime.ImportExpander.StatusText.AddSuccess"));
            }
            catch (Exception ex)
            {
                Dispatcher.UIThread.Invoke(() => SearchStatus.Text = I18nManager.Instance.GetString("Pages.SettingsPage.GameSettingsPage.JavaRuntime.ImportExpander.StatusText.AddFailed", ex.Message));
                s_logger.Error(ex, "添加 Java");
            }
            await RefreshJavaItems();
        });
    }
    void AutoSelectJava_Changed(object? sender, RoutedEventArgs e) {
        Current.Config.AutoSelectJava = AutoSelectJavaToggleSwitch.IsChecked ?? true;
        ConfigManager.Save("app", Current.Config);
        AutoSelectJavaToggleSwitch.IsChecked = Current.Config.AutoSelectJava;
    }
}

public record JavaItem {
    public string Path { get; set; } 
    public string Header { get; set; }
    public IBrush Foreground { get; set; }
    public bool IsSelected { get; set; }
}