// Copyright 2025-2026 LinearTeam
// 
//    Licensed under the Apache License, Version 2.0 (the "License");
//    you may not use this file except in compliance with the License.
//    You may obtain a copy of the License at
// 
//        http://www.apache.org/licenses/LICENSE-2.0
// 
//    Unless required by applicable law or agreed to in writing, software
//    distributed under the License is distributed on an "AS IS" BASIS,
//    WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//    See the License for the specific language governing permissions and
//    limitations under the License.

using Avalonia.Controls;
using Avalonia.Controls.Primitives;

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
using LMCCore.Game.Launching.Configuration;
using Utils;

public partial class GameSettingsPage : PageBase {
    private ObservableCollection<JavaItem> _javaItems = new();
    private readonly Logger _logger = new Logger("GameSettingsPage");
    private readonly DispatcherTimer _launchSettingsSaveTimer;
    private readonly DispatcherTimer _memoryInfoTimer;
    private bool _isLoadingLaunchSettings;
    private bool _hasPendingLaunchSettingsSave;
    private bool _isSavingLaunchSettings;
    private bool _isPageLoaded;
    private bool _isUpdatingMemoryControls;

    public GameSettingsPage() : base(I18nManager.Instance.GetString("Pages.SettingsPage.GameSettingsPage.Title"), "GameSettingsPage") {
        InitializeComponent();
        _launchSettingsSaveTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(400)
        };
        _launchSettingsSaveTimer.Tick += LaunchSettingsSaveTimer_OnTick;
        _memoryInfoTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _memoryInfoTimer.Tick += MemoryInfoTimer_OnTick;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }
    async private void OnLoaded(object? sender, RoutedEventArgs e) {
        _isPageLoaded = true;
        await LoadConfigs();
        if (!_isPageLoaded)
        {
            return;
        }

        SearchStatus.Text = "";
        _memoryInfoTimer.Start();
    }

    private void OnUnloaded(object? sender, RoutedEventArgs e)
    {
        _isPageLoaded = false;
        _memoryInfoTimer.Stop();
        FlushLaunchSettingsSave();
    }

    async private Task LoadConfigs() {
        await RefreshJavaItems();
        AutoSelectJavaToggleSwitch.IsChecked = Current.Config.AutoSelectJava;
        LoadLaunchSettings();
    }

    async private Task RefreshJavaItems()
    {
        try
        {
            var javaPaths = Current.Config.JavaPaths.ToArray();
            var javaInfos = new List<LocalJava>(javaPaths.Length);
            var invalidPaths = new List<string>();
            foreach (var javaPath in javaPaths)
            {
                try
                {
                    var javaInfo = await JavaManager.TryGetJavaInfo(javaPath);
                    if (javaInfo != null)
                    {
                        javaInfos.Add(javaInfo);
                        continue;
                    }

                    invalidPaths.Add(javaPath);
                }
                catch (Exception ex)
                {
                    invalidPaths.Add(javaPath);
                    _logger.Warn($"忽略失效 Java 路径 {javaPath}: {ex.Message}");
                }
            }

            if (invalidPaths.Count > 0)
            {
                foreach (var invalidPath in invalidPaths)
                {
                    Current.Config.JavaPaths.Remove(invalidPath);
                }

                if (invalidPaths.Any(path => string.Equals(
                        path,
                        Current.Config.SelectedJavaPath,
                        StringComparison.OrdinalIgnoreCase)))
                {
                    Current.Config.SelectedJavaPath = string.Empty;
                }

                await Task.Run(() => ConfigManager.Save("app", Current.Config));
            }

            var items = GameSettingsPagePresentation.BuildJavaItems(
                GameSettingsPagePresentation.BuildJavaItemViewData(
                    javaInfos,
                    Current.Config.SelectedJavaPath,
                    I18nManager.Instance.GetString("Pages.SettingsPage.GameSettingsPage.JavaRuntime.JavaListExpander.JavaListItem.Enabled")),
                Brushes.LawnGreen,
                Foreground!);

            jle.Header = I18nManager.Instance.GetString(GameSettingsPagePresentation.GetJavaListHeaderKey(items.Count));
            _javaItems = new ObservableCollection<JavaItem>(items);
            jle.ItemsSource = _javaItems;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Refreshing Java Items");
        }
    }
    async private void SelectJava_Click(object? sender, RoutedEventArgs e) {
        if (sender is not Button { Tag: string java })
        {
            return;
        }

        _logger.Info($"用户选择Java: {java}");
        
        if (Current.Config.JavaPaths.Contains(java))
        {
            Current.Config.SelectedJavaPath = java;
            await Task.Run(() => ConfigManager.Save("app", Current.Config));
            _logger.Info("选择成功");
            await RefreshJavaItems();
            return;
        }
        _logger.Warn("选择失败 (1)");
        await RefreshJavaItems();
    }
    async private void RemoveJava_Click(object? sender, RoutedEventArgs e) {
        if (sender is not Button { Tag: string java })
        {
            return;
        }

        _logger.Info($"用户移除Java: {java}");
        
        if (Current.Config.JavaPaths.Contains(java))
        {
            await Task.Run(() => JavaManager.RemoveJava(java));
            _logger.Info("移除成功");
            await RefreshJavaItems();
            return;
        }
        _logger.Warn("移除失败 (1)");
        await RefreshJavaItems();
        
    }
    async private void SearchJava_Click(object? sender, RoutedEventArgs e) {
        var progress = new Action<TaskCallbackInfo>(info => {
            Dispatcher.UIThread.Post(() =>
            {
                SearchStatus.Text = I18nManager.Instance.GetString(
                    "Pages.SettingsPage.GameSettingsPage.JavaRuntime.ImportExpander.StatusText.SearchProgress",
                    info.Progress,
                    info.Total,
                    I18nManager.Instance.GetString(info.Message));
            });
        });

        var button = (Button)sender!;
        button.IsEnabled = false;
        
        try {
            var javas = await Task.Run(() => JavaManager.SearchJava(progress));
            await Task.Run(() => JavaManager.AddJavasAsync(javas));
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
    private void JavaItemExpander_Click(object? sender, RoutedEventArgs e) {
        if (sender is not FASettingsExpanderItem { Tag: string path })
        {
            return;
        }

        path = Path.GetFullPath(path);
        CrossPlatformUtils.OpenFolderInExplorer(path);
    }
    async private void AddJava_Click(object? sender, RoutedEventArgs e)
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
            var root = GameSettingsPagePresentation.ResolveJavaRootPath(file.Path.LocalPath);

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
    async private void AutoSelectJava_Changed(object? sender, RoutedEventArgs e)
    {
        var isChecked = AutoSelectJavaToggleSwitch.IsChecked ?? true;

        Current.Config.AutoSelectJava = isChecked;
        ScheduleLaunchSettingsSave();
    }

    private void LoadLaunchSettings()
    {
        _isLoadingLaunchSettings = true;
        try
        {
            var launchConfig = Current.Config.GameLaunch ??= new GameLaunchConfig();
            UpdateMemoryUsage();
            SetMemoryControlsValue(launchConfig.MaxMemoryMb);
            AutoAllocateMemoryToggle.IsChecked = launchConfig.AutoAllocateMemory;
            JvmArgumentsTextBox.Text = launchConfig.JvmArguments;
            WrapperArgumentsTextBox.Text = launchConfig.WrapperArguments;
            GameArgumentsTextBox.Text = launchConfig.GameArguments;
            UpdateMemoryControlState();
        }
        finally
        {
            _isLoadingLaunchSettings = false;
        }
    }

    private void MemoryInfoTimer_OnTick(object? sender, EventArgs e)
    {
        UpdateMemoryUsage();
    }

    private void UpdateMemoryUsage()
    {
        var memoryInfo = SystemMemoryInfoProvider.GetCurrent();
        var totalMemoryMb = Math.Max(1024, memoryInfo.TotalBytes / 1024 / 1024);
        MemorySettingsExpander.Description = I18nManager.Instance.GetString(
            "Pages.SettingsPage.GameSettingsPage.LaunchSettings.Memory.DescriptionWithUsage",
            totalMemoryMb,
            memoryInfo.UsedBytes / 1024 / 1024,
            memoryInfo.AvailableBytes / 1024 / 1024,
            GetMemoryUsagePercentage(memoryInfo));
        var maximum = Math.Max(512, totalMemoryMb);
        _isUpdatingMemoryControls = true;
        try
        {
            MaxMemorySlider.Maximum = maximum;
            MaxMemoryNumericUpDown.Maximum = maximum;
        }
        finally
        {
            _isUpdatingMemoryControls = false;
        }
    }

    private static double GetMemoryUsagePercentage(SystemMemoryInfo memoryInfo)
    {
        return memoryInfo.TotalBytes <= 0
            ? 0
            : memoryInfo.UsedBytes * 100d / memoryInfo.TotalBytes;
    }

    private void AutoAllocateMemoryToggle_OnIsCheckedChanged(object? sender, RoutedEventArgs e)
    {
        if (_isLoadingLaunchSettings)
        {
            return;
        }

        var launchConfig = Current.Config.GameLaunch ??= new GameLaunchConfig();
        launchConfig.AutoAllocateMemory = AutoAllocateMemoryToggle.IsChecked ?? true;
        UpdateMemoryControlState();
        ScheduleLaunchSettingsSave();
    }

    private void MaxMemorySlider_OnValueChanged(object? sender, RangeBaseValueChangedEventArgs e)
    {
        if (_isLoadingLaunchSettings || _isUpdatingMemoryControls)
        {
            return;
        }

        ApplyMemoryValue((int)Math.Round(e.NewValue));
    }

    private void JvmArgumentsTextBox_OnTextChanged(object? sender, TextChangedEventArgs e)
    {
        UpdateLaunchArgumentConfig(config => config.JvmArguments = JvmArgumentsTextBox.Text ?? string.Empty);
    }

    private void WrapperArgumentsTextBox_OnTextChanged(object? sender, TextChangedEventArgs e)
    {
        UpdateLaunchArgumentConfig(config => config.WrapperArguments = WrapperArgumentsTextBox.Text ?? string.Empty);
    }

    private void GameArgumentsTextBox_OnTextChanged(object? sender, TextChangedEventArgs e)
    {
        UpdateLaunchArgumentConfig(config => config.GameArguments = GameArgumentsTextBox.Text ?? string.Empty);
    }

    private void UpdateLaunchArgumentConfig(Action<GameLaunchConfig> update)
    {
        if (_isLoadingLaunchSettings)
        {
            return;
        }

        var launchConfig = Current.Config.GameLaunch ??= new GameLaunchConfig();
        update(launchConfig);
        ScheduleLaunchSettingsSave();
    }

    private void UpdateMemoryControlState()
    {
        var isManualAllocationEnabled = !(AutoAllocateMemoryToggle.IsChecked ?? true);
        MemoryManualSettingsItem.IsEnabled = isManualAllocationEnabled;
        MaxMemorySlider.IsEnabled = isManualAllocationEnabled;
        MaxMemoryNumericUpDown.IsEnabled = isManualAllocationEnabled;
    }

    private void ScheduleLaunchSettingsSave()
    {
        _hasPendingLaunchSettingsSave = true;
        _launchSettingsSaveTimer.Stop();
        _launchSettingsSaveTimer.Start();
    }

    private void LaunchSettingsSaveTimer_OnTick(object? sender, EventArgs e)
    {
        _launchSettingsSaveTimer.Stop();
        FlushLaunchSettingsSave();
    }

    private void FlushLaunchSettingsSave()
    {
        _launchSettingsSaveTimer.Stop();
        if (!_hasPendingLaunchSettingsSave || _isSavingLaunchSettings)
        {
            return;
        }

        _isSavingLaunchSettings = true;
        _ = SaveLaunchSettingsAsync();
    }

    async private Task SaveLaunchSettingsAsync()
    {
        try
        {
            do
            {
                _hasPendingLaunchSettingsSave = false;
                await Task.Run(() => ConfigManager.Save("app", Current.Config));
            } while (_hasPendingLaunchSettingsSave);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Saving game launch settings");
        }
        finally
        {
            _isSavingLaunchSettings = false;
            if (_hasPendingLaunchSettingsSave)
            {
                FlushLaunchSettingsSave();
            }
        }
    }
    private void MaxMemoryNumericUpDown_OnValueChanged(object? sender, NumericUpDownValueChangedEventArgs e)
    {
        if (_isLoadingLaunchSettings || _isUpdatingMemoryControls || e.NewValue is null)
        {
            return;
        }

        ApplyMemoryValue((int)Math.Round(e.NewValue.Value));
    }

    private void SetMemoryControlsValue(int memoryMb)
    {
        var clampedValue = Math.Clamp(
            memoryMb,
            (int)MaxMemorySlider.Minimum,
            (int)MaxMemorySlider.Maximum);

        _isUpdatingMemoryControls = true;
        try
        {
            MaxMemorySlider.Value = clampedValue;
            MaxMemoryNumericUpDown.Value = clampedValue;
        }
        finally
        {
            _isUpdatingMemoryControls = false;
        }
    }

    private void ApplyMemoryValue(int memoryMb)
    {
        var clampedValue = Math.Clamp(
            memoryMb,
            (int)MaxMemorySlider.Minimum,
            (int)MaxMemorySlider.Maximum);
        SetMemoryControlsValue(clampedValue);

        var launchConfig = Current.Config.GameLaunch ??= new GameLaunchConfig();
        launchConfig.MaxMemoryMb = clampedValue;
        ScheduleLaunchSettingsSave();
    }
}

public record JavaItem {
    public string Path { get; set; } 
    public string Header { get; set; }
    public IBrush Foreground { get; set; }
    public bool IsSelected { get; set; }
}
