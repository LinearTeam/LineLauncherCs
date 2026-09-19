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

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Avalonia.VisualTree;
using FluentAvalonia.UI.Controls;
using LMC;
using LMC.Basic;
using LMC.Basic.Configs;
using LMC.Basic.Logging;
using LMCCore.Game.Launching.Configuration;
using LMCCore.Game.Model;
using LMCCore.Game.Model.Configuration;
using LMCCore.Game.Versioning;
using LMCCore.Java;
using LMCCore.Utils;
using LMCUI.I18n;
using LMCUI.Pages;
using LMCUI.Pages.SettingsPage.GameSettings;
using LMCUI.Utils;

namespace LMCUI.Pages.VersionManagePage;

public partial class VersionGameSettingsPage : PageBase
{
    private readonly Logger _logger = new("VersionGameSettingsPage");
    private readonly VersionConfigManager _versionConfigManager = new();
    private readonly DispatcherTimer _configWriteTimer;
    private readonly DispatcherTimer _memoryInfoTimer;
    private LocalGameVersionEntry? _version;
    private GameVersionConfig _independentConfig = new();
    private VersionConfigWriteRequest? _pendingConfigWrite;
    private bool _isLoadingSettings;
    private bool _isUpdatingMemoryControls;
    private bool _isSavingConfig;

    public VersionGameSettingsPage() : base("Pages.VersionGameSettingsPage.Title", "VersionGameSettingsPage")
    {
        InitializeComponent();
        _configWriteTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(400) };
        _configWriteTimer.Tick += ConfigWriteTimer_OnTick;
        _memoryInfoTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _memoryInfoTimer.Tick += MemoryInfoTimer_OnTick;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    public override void ProcessParameter(object? param)
    {
        base.ProcessParameter(param);
        if (param is not LocalGameVersionEntry version)
        {
            return;
        }

        FlushConfigWrite();
        _version = version;
        _independentConfig = _versionConfigManager.GetIndependentConfigModel(version) ?? new GameVersionConfig();
        Title = I18nManager.Instance.GetString("Pages.VersionGameSettingsPage.Title", version.VersionName);
        LoadLaunchSettings();
    }

    private void LoadLaunchSettings()
    {
        if (_version == null)
        {
            return;
        }

        _isLoadingSettings = true;
        try
        {
            UseGlobalGameSettingsToggle.IsChecked = _independentConfig.UseGlobalGameSettings;
            var settings = ResolveLaunchSettings();
            UpdateMemoryUsage();
            AutoSelectJavaToggleSwitch.IsChecked = settings.AutoSelectJava;
            SetMemoryControlsValue(settings.MaxMemoryMb);
            AutoAllocateMemoryToggle.IsChecked = settings.AutoAllocateMemory;
            JvmArgumentsTextBox.Text = settings.JvmArguments;
            WrapperArgumentsTextBox.Text = settings.WrapperArguments;
            GameArgumentsTextBox.Text = settings.GameArguments;
            UpdateMemoryControlState();
            UpdateVersionSettingsControlState();
        }
        finally
        {
            _isLoadingSettings = false;
        }
    }

    private async void OnLoaded(object? sender, RoutedEventArgs e)
    {
        _memoryInfoTimer.Start();
        LoadLaunchSettings();
        await RefreshJavaItems();
    }

    private void MemoryInfoTimer_OnTick(object? sender, EventArgs e) => UpdateMemoryUsage();

    private void UpdateMemoryUsage()
    {
        if (_version == null)
        {
            return;
        }

        var memoryInfo = SystemMemoryInfoProvider.GetCurrent();
        var totalMemoryMb = Math.Max(1024, memoryInfo.TotalBytes / 1024 / 1024);
        MemorySettingsExpander.Description = I18nManager.Instance.GetString(
            "Pages.VersionDetailPage.LaunchSettings.Memory.DescriptionWithUsage",
            totalMemoryMb,
            memoryInfo.UsedBytes / 1024 / 1024,
            memoryInfo.AvailableBytes / 1024 / 1024,
            memoryInfo.TotalBytes <= 0 ? 0 : memoryInfo.UsedBytes * 100d / memoryInfo.TotalBytes);
        _isUpdatingMemoryControls = true;
        try
        {
            MaxMemorySlider.Maximum = Math.Max(512, totalMemoryMb);
            MaxMemoryNumericUpDown.Maximum = Math.Max(512, totalMemoryMb);
        }
        finally
        {
            _isUpdatingMemoryControls = false;
        }
    }

    private async Task RefreshJavaItems()
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
                    }
                    else
                    {
                        invalidPaths.Add(javaPath);
                    }
                }
                catch (Exception ex)
                {
                    invalidPaths.Add(javaPath);
                    _logger.Warn($"Ignoring invalid Java path {javaPath}: {ex.Message}");
                }
            }

            if (invalidPaths.Count > 0)
            {
                foreach (var invalidPath in invalidPaths)
                {
                    Current.Config.JavaPaths.Remove(invalidPath);
                }

                if (invalidPaths.Any(path => string.Equals(path, Current.Config.SelectedJavaPath,
                        StringComparison.OrdinalIgnoreCase)))
                {
                    Current.Config.SelectedJavaPath = string.Empty;
                }

                await Task.Run(() => ConfigManager.Save("app", Current.Config));
            }

            if (!_independentConfig.UseGlobalGameSettings &&
                _independentConfig.Java?.SelectedJavaPath is { } versionJavaPath &&
                invalidPaths.Any(path => string.Equals(path, versionJavaPath, StringComparison.OrdinalIgnoreCase)))
            {
                EnsureJavaConfig().SelectedJavaPath = null;
                QueueConfigWrite();
            }

            var items = GameSettingsPagePresentation.BuildJavaItems(
                GameSettingsPagePresentation.BuildJavaItemViewData(
                    javaInfos,
                    ResolveLaunchSettings().SelectedJavaPath,
                    I18nManager.Instance.GetString(
                        "Pages.SettingsPage.GameSettingsPage.JavaRuntime.JavaListExpander.JavaListItem.Enabled")),
                Brushes.LawnGreen,
                Foreground!);
            JavaListExpander.Header = I18nManager.Instance.GetString(
                GameSettingsPagePresentation.GetJavaListHeaderKey(items.Count));
            JavaListExpander.ItemsSource = new ObservableCollection<JavaItem>(items);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Refreshing version Java items");
        }
    }

    private async void SelectJava_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string java })
        {
            return;
        }

        if (!_independentConfig.UseGlobalGameSettings && Current.Config.JavaPaths.Contains(java))
        {
            EnsureJavaConfig().SelectedJavaPath = java;
            QueueConfigWrite();
        }

        await RefreshJavaItems();
    }

    private async void RemoveJava_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string java } && Current.Config.JavaPaths.Contains(java))
        {
            await Task.Run(() => JavaManager.RemoveJava(java));
        }

        await RefreshJavaItems();
    }

    private async void SearchJava_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button button)
        {
            return;
        }

        button.IsEnabled = false;
        try
        {
            var progress = new Action<TaskCallbackInfo>(info => Dispatcher.UIThread.Post(() =>
                JavaSearchStatus.Text = I18nManager.Instance.GetString(
                    "Pages.SettingsPage.GameSettingsPage.JavaRuntime.ImportExpander.StatusText.SearchProgress",
                    info.Progress, info.Total, I18nManager.Instance.GetString(info.Message))));
            var javas = await Task.Run(() => JavaManager.SearchJava(progress));
            await Task.Run(() => JavaManager.AddJavasAsync(javas));
            JavaSearchStatus.Text = I18nManager.Instance.GetString(
                "Pages.SettingsPage.GameSettingsPage.JavaRuntime.ImportExpander.StatusText.SearchSuccess");
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Searching Java for version");
            JavaSearchStatus.Text = I18nManager.Instance.GetString(
                "Pages.SettingsPage.GameSettingsPage.JavaRuntime.ImportExpander.StatusText.SearchFailed", ex.Message);
        }
        finally
        {
            await RefreshJavaItems();
            button.IsEnabled = true;
        }
    }

    private async void AddJava_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            var typeFilter = new FilePickerFileType(RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "java.exe" : "java")
            {
                Patterns = [RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "java.exe" : "java"]
            };
            var file = (await CrossPlatformUtils.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                AllowMultiple = false,
                FileTypeFilter = [typeFilter],
                Title = I18nManager.Instance.GetString(
                    "Pages.SettingsPage.GameSettingsPage.JavaRuntime.ImportExpander.ChooseJavaDialog.Title")
            })).FirstOrDefault();
            if (file == null)
            {
                return;
            }

            await Task.Run(() => JavaManager.AddJava(
                GameSettingsPagePresentation.ResolveJavaRootPath(file.Path.LocalPath)));
            JavaSearchStatus.Text = I18nManager.Instance.GetString(
                "Pages.SettingsPage.GameSettingsPage.JavaRuntime.ImportExpander.StatusText.AddSuccess");
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Adding Java for version");
            JavaSearchStatus.Text = I18nManager.Instance.GetString(
                "Pages.SettingsPage.GameSettingsPage.JavaRuntime.ImportExpander.StatusText.AddFailed", ex.Message);
        }

        await RefreshJavaItems();
    }

    private void JavaItemExpander_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is FASettingsExpanderItem { Tag: string path })
        {
            CrossPlatformUtils.OpenFolderInExplorer(Path.GetFullPath(path));
        }
    }

    private void AutoSelectJava_Changed(object? sender, RoutedEventArgs e)
    {
        if (!_isLoadingSettings)
        {
            EnsureJavaConfig().AutoSelectJava = AutoSelectJavaToggleSwitch.IsChecked ?? true;
            QueueConfigWrite();
        }
    }

    private void UseGlobalGameSettingsToggle_OnIsCheckedChanged(object? sender, RoutedEventArgs e)
    {
        if (_isLoadingSettings)
        {
            return;
        }

        _independentConfig.UseGlobalGameSettings = UseGlobalGameSettingsToggle.IsChecked ?? true;
        UpdateVersionSettingsControlState();
        LoadLaunchSettings();
        QueueConfigWrite();
    }

    private void AutoAllocateMemoryToggle_OnIsCheckedChanged(object? sender, RoutedEventArgs e)
    {
        if (!_isLoadingSettings)
        {
            EnsureJavaConfig().AutoAllocateMemory = AutoAllocateMemoryToggle.IsChecked ?? true;
            UpdateMemoryControlState();
            QueueConfigWrite();
        }
    }

    private void MaxMemorySlider_OnValueChanged(object? sender, RangeBaseValueChangedEventArgs e)
    {
        if (!_isLoadingSettings && !_isUpdatingMemoryControls)
        {
            ApplyMemoryValue((int)Math.Round(e.NewValue));
        }
    }

    private void MaxMemoryNumericUpDown_OnValueChanged(object? sender, NumericUpDownValueChangedEventArgs e)
    {
        if (!_isLoadingSettings && !_isUpdatingMemoryControls && e.NewValue is { } newValue)
        {
            ApplyMemoryValue((int)Math.Round(newValue));
        }
    }

    private void SetMemoryControlsValue(int memoryMb)
    {
        var clampedValue = Math.Clamp(memoryMb, (int)MaxMemorySlider.Minimum, (int)MaxMemorySlider.Maximum);
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
        var clampedValue = Math.Clamp(memoryMb, (int)MaxMemorySlider.Minimum, (int)MaxMemorySlider.Maximum);
        SetMemoryControlsValue(clampedValue);
        EnsureJavaConfig().MaxMemoryMb = clampedValue;
        QueueConfigWrite();
    }

    private void JvmArgumentsTextBox_OnTextChanged(object? sender, TextChangedEventArgs e)
    {
        if (!_isLoadingSettings)
        {
            EnsureJavaConfig().JvmArguments = JvmArgumentsTextBox.Text ?? string.Empty;
            QueueConfigWrite();
        }
    }

    private void WrapperArgumentsTextBox_OnTextChanged(object? sender, TextChangedEventArgs e)
    {
        if (!_isLoadingSettings)
        {
            EnsureJavaConfig().WrapperArguments = WrapperArgumentsTextBox.Text ?? string.Empty;
            QueueConfigWrite();
        }
    }

    private void GameArgumentsTextBox_OnTextChanged(object? sender, TextChangedEventArgs e)
    {
        if (!_isLoadingSettings)
        {
            (_independentConfig.Game ??= new GameVersionGameConfig()).GameArguments = GameArgumentsTextBox.Text ?? string.Empty;
            QueueConfigWrite();
        }
    }

    private GameVersionJavaConfig EnsureJavaConfig() => _independentConfig.Java ??= new GameVersionJavaConfig();

    private void UpdateMemoryControlState()
    {
        var isManualAllocationEnabled = !(AutoAllocateMemoryToggle.IsChecked ?? true);
        MemoryManualSettingsItem.IsEnabled = isManualAllocationEnabled;
        MaxMemorySlider.IsEnabled = isManualAllocationEnabled;
        MaxMemoryNumericUpDown.IsEnabled = isManualAllocationEnabled;
    }

    private void UpdateVersionSettingsControlState()
    {
        var independentSettingsEnabled = !_independentConfig.UseGlobalGameSettings;
        JavaListExpander.IsEnabled = independentSettingsEnabled;
        ImportJavaExpander.IsEnabled = independentSettingsEnabled;
        AutoSelectJavaExpander.IsEnabled = independentSettingsEnabled;
        MemorySettingsExpander.IsEnabled = independentSettingsEnabled;
        ArgumentsSettingsExpander.IsEnabled = independentSettingsEnabled;
        UpdateExpansionChevronVisibility(JavaListExpander, independentSettingsEnabled);
        UpdateExpansionChevronVisibility(MemorySettingsExpander, independentSettingsEnabled);
        UpdateExpansionChevronVisibility(ArgumentsSettingsExpander, independentSettingsEnabled);
    }

    private static void UpdateExpansionChevronVisibility(FASettingsExpander expander, bool isEnabled)
    {
        var chevron = expander.GetVisualDescendants()
            .OfType<FASymbolIcon>()
            .FirstOrDefault(icon => icon.Name == "ExpandCollapseChevron");
        if (chevron != null)
        {
            chevron.IsVisible = isEnabled;
        }
    }

    private GameLaunchSettings ResolveLaunchSettings() => GameLaunchSettingsResolver.Resolve(
        Current.Config,
        _version == null ? null : _versionConfigManager.GetEffectiveConfigModel(_version));

    private void QueueConfigWrite()
    {
        if (_version == null)
        {
            return;
        }

        _pendingConfigWrite = new VersionConfigWriteRequest(
            _version,
            JsonSerializer.SerializeToNode(_independentConfig, JsonUtils.DefaultSerializerOptions)
            ?? throw new InvalidOperationException("Unable to serialize version launch settings."),
            ResolveWriteSource(_version));
        _configWriteTimer.Stop();
        _configWriteTimer.Start();
    }

    private void OnUnloaded(object? sender, RoutedEventArgs e)
    {
        _memoryInfoTimer.Stop();
        FlushConfigWrite();
    }

    private void ConfigWriteTimer_OnTick(object? sender, EventArgs e)
    {
        _configWriteTimer.Stop();
        FlushConfigWrite();
    }

    private void FlushConfigWrite()
    {
        _configWriteTimer.Stop();
        if (_pendingConfigWrite == null || _isSavingConfig)
        {
            return;
        }

        _isSavingConfig = true;
        _ = SaveConfigAsync();
    }

    private async Task SaveConfigAsync()
    {
        try
        {
            while (_pendingConfigWrite is { } request)
            {
                _pendingConfigWrite = null;
                await Task.Run(() => _versionConfigManager.WriteIndependentConfig(
                    request.Version, request.Config, request.Source));
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Saving version launch settings");
        }
        finally
        {
            _isSavingConfig = false;
            if (_pendingConfigWrite != null)
            {
                FlushConfigWrite();
            }
        }
    }

    private VersionConfigSourceType ResolveWriteSource(LocalGameVersionEntry version)
    {
        var source = _versionConfigManager.GetConfigSource(version);
        if (source != VersionConfigSourceType.None)
        {
            return source;
        }

        return Current.Config.DefaultVersionConfigSourceForNewInstalls switch
        {
            NewVersionConfigSource.VersionJson => VersionConfigSourceType.VersionJson,
            NewVersionConfigSource.VersionFolder => VersionConfigSourceType.VersionFolder,
            NewVersionConfigSource.LMCDataDirectory => VersionConfigSourceType.LMCDataDirectory,
            _ => VersionConfigSourceType.VersionJson
        };
    }

    private sealed record VersionConfigWriteRequest(
        LocalGameVersionEntry Version,
        JsonNode Config,
        VersionConfigSourceType Source);
}
