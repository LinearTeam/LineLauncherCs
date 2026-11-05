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
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Threading;
using FluentAvalonia.UI.Controls;
using LMC.Basic.Logging;
using LMCCore.Game.Launching.Configuration;
using LMCUI.I18n;

namespace LMCUI.Controls.GameSettings;

public partial class GameSettingsEditor : UserControl
{
    private readonly Logger _logger = new("GameSettingsEditor");
    private readonly DispatcherTimer _memoryInfoTimer;
    private GameLaunchSettings _settings = new(true, string.Empty, true, 4096, "", "", "");
    private bool _isLoadingSettings = true;
    private bool _isUpdatingMemoryControls;
    private bool _isReadOnly;

    public bool IsVersionSettings { get; set; }
    public event EventHandler<GameSettingsChangedEventArgs>? SettingsChanged;
    public event Action<IReadOnlyList<string>>? InvalidJavaPathsFound;

    private string LaunchSettingsKey => IsVersionSettings
        ? "Pages.VersionDetailPage.LaunchSettings"
        : "Pages.SettingsPage.GameSettingsPage.LaunchSettings";

    public GameSettingsEditor()
    {
        InitializeComponent();
        _isLoadingSettings = false;
        _memoryInfoTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _memoryInfoTimer.Tick += (_, _) => UpdateMemoryUsage();
        Loaded += OnLoaded;
        Unloaded += (_, _) => _memoryInfoTimer.Stop();
    }

    public void LoadSettings(GameLaunchSettings settings, bool isReadOnly = false)
    {
        _isLoadingSettings = true;
        try
        {
            _settings = settings;
            _isReadOnly = isReadOnly;
            ApplyLabels();
            UpdateMemoryUsage();
            AutoSelectJavaToggleSwitch.IsChecked = settings.AutoSelectJava;
            SetMemoryControlsValue(settings.MaxMemoryMb);
            AutoAllocateMemoryToggle.IsChecked = settings.AutoAllocateMemory;
            JvmArgumentsTextBox.Text = settings.JvmArguments;
            WrapperArgumentsTextBox.Text = settings.WrapperArguments;
            GameArgumentsTextBox.Text = settings.GameArguments;
            foreach (var group in new[]
                     {
                         JavaListExpander, ImportJavaExpander, AutoSelectJavaExpander,
                         MemorySettingsExpander, ArgumentsSettingsExpander
                     })
            {
                group.IsEnabled = !isReadOnly;
            }

            UpdateMemoryControlState();
            UpdateJavaItems();
        }
        finally
        {
            _isLoadingSettings = false;
        }
    }

    private async void OnLoaded(object? sender, RoutedEventArgs e)
    {
        UpdateMemoryUsage();
        _memoryInfoTimer.Start();
        JavaSearchStatus.Text = string.Empty;
        await RefreshJavaItemsAsync();
    }

    private void ApplyLabels()
    {
        string Text(string suffix) => I18nManager.Instance.GetString($"{LaunchSettingsKey}.{suffix}");

        JavaRuntimeLabel.Content = I18nManager.Instance.GetString(IsVersionSettings
            ? "Pages.VersionDetailPage.JavaRuntime.Title"
            : "Pages.SettingsPage.GameSettingsPage.JavaRuntime.Title");
        LaunchSettingsLabel.Content = Text("Title");
        MemorySettingsExpander.Header = Text("Memory.Header");
        AutoAllocateMemoryToggle.OnContent = Text("Memory.AutoAllocate.On");
        AutoAllocateMemoryToggle.OffContent = Text("Memory.AutoAllocate.Off");
        MemoryAllocationLabel.Text = Text("Memory.ManualAllocation");
        ArgumentsSettingsExpander.Header = Text("Arguments.Header");
        ArgumentsSettingsExpander.Description = Text("Arguments.Description");
        SetArgumentLabels(JvmArgumentsItem, "Jvm");
        SetArgumentLabels(WrapperArgumentsItem, "Wrapper");
        SetArgumentLabels(GameArgumentsItem, "Game");

        void SetArgumentLabels(FASettingsExpanderItem item, string name)
        {
            item.Content = Text($"Arguments.{name}.Header");
            item.Description = Text($"Arguments.{name}.Description");
        }
    }

    private void UpdateMemoryUsage()
    {
        var memoryInfo = SystemMemoryInfoProvider.GetCurrent();
        var totalMemoryMb = Math.Max(1024, memoryInfo.TotalBytes / 1024 / 1024);
        MemorySettingsExpander.Description = I18nManager.Instance.GetString(
            $"{LaunchSettingsKey}.Memory.DescriptionWithUsage",
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

    private void ChangeSetting(GameSettingsField field, GameLaunchSettings settings)
    {
        if (_isLoadingSettings || _isReadOnly || settings == _settings)
        {
            return;
        }

        _settings = settings;
        SettingsChanged?.Invoke(this, new GameSettingsChangedEventArgs(field, settings));
    }

    private void AutoSelectJava_Changed(object? sender, RoutedEventArgs e)
    {
        ChangeSetting(GameSettingsField.AutoSelectJava,
            _settings with { AutoSelectJava = AutoSelectJavaToggleSwitch.IsChecked ?? true });
    }

    private void AutoAllocateMemoryToggle_OnIsCheckedChanged(object? sender, RoutedEventArgs e)
    {
        if (_isLoadingSettings)
        {
            return;
        }

        ChangeSetting(GameSettingsField.AutoAllocateMemory,
            _settings with { AutoAllocateMemory = AutoAllocateMemoryToggle.IsChecked ?? true });
        UpdateMemoryControlState();
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
        if (!_isLoadingSettings && !_isUpdatingMemoryControls && e.NewValue is { } value)
        {
            ApplyMemoryValue((int)Math.Round(value));
        }
    }

    private void SetMemoryControlsValue(int memoryMb)
    {
        var value = Math.Clamp(memoryMb, (int)MaxMemorySlider.Minimum, (int)MaxMemorySlider.Maximum);
        _isUpdatingMemoryControls = true;
        try
        {
            MaxMemorySlider.Value = value;
            MaxMemoryNumericUpDown.Value = value;
        }
        finally
        {
            _isUpdatingMemoryControls = false;
        }
    }

    private void ApplyMemoryValue(int memoryMb)
    {
        var value = Math.Clamp(memoryMb, (int)MaxMemorySlider.Minimum, (int)MaxMemorySlider.Maximum);
        SetMemoryControlsValue(value);
        ChangeSetting(GameSettingsField.MaxMemoryMb, _settings with { MaxMemoryMb = value });
    }

    private void UpdateMemoryControlState()
    {
        MemoryManualSettingsItem.IsEnabled = !(AutoAllocateMemoryToggle.IsChecked ?? true);
    }

    private void JvmArgumentsTextBox_OnTextChanged(object? sender, TextChangedEventArgs e)
    {
        ChangeSetting(GameSettingsField.JvmArguments,
            _settings with { JvmArguments = JvmArgumentsTextBox.Text ?? string.Empty });
    }

    private void WrapperArgumentsTextBox_OnTextChanged(object? sender, TextChangedEventArgs e)
    {
        ChangeSetting(GameSettingsField.WrapperArguments,
            _settings with { WrapperArguments = WrapperArgumentsTextBox.Text ?? string.Empty });
    }

    private void GameArgumentsTextBox_OnTextChanged(object? sender, TextChangedEventArgs e)
    {
        ChangeSetting(GameSettingsField.GameArguments,
            _settings with { GameArguments = GameArgumentsTextBox.Text ?? string.Empty });
    }
}
