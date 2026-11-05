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
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Avalonia.Interactivity;
using Avalonia.Threading;
using LMC;
using LMC.Basic.Configs;
using LMC.Basic.Logging;
using LMCCore.Game.Launching.Configuration;
using LMCCore.Game.Model;
using LMCCore.Game.Model.Configuration;
using LMCCore.Game.Versioning;
using LMCCore.Utils;
using LMCUI.Controls.GameSettings;
using LMCUI.I18n;

namespace LMCUI.Pages.VersionManagePage;

public partial class VersionGameSettingsPage : PageBase
{
    private readonly Logger _logger = new("VersionGameSettingsPage");
    private readonly VersionConfigManager _versionConfigManager = new();
    private readonly DispatcherTimer _configWriteTimer;
    private LocalGameVersionEntry? _version;
    private GameVersionConfig _independentConfig = new();
    private VersionConfigWriteRequest? _pendingConfigWrite;
    private bool _isLoadingSettings;
    private bool _isSavingConfig;

    public VersionGameSettingsPage() : base("Pages.VersionGameSettingsPage.Title", "VersionGameSettingsPage")
    {
        InitializeComponent();
        _configWriteTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(400) };
        _configWriteTimer.Tick += ConfigWriteTimer_OnTick;
        SettingsEditor.InvalidJavaPathsFound += OnInvalidJavaPathsFound;
        Loaded += (_, _) => LoadLaunchSettings();
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
            var effectiveConfig = _versionConfigManager.GetEffectiveConfigModel(_version, _independentConfig);
            SettingsEditor.LoadSettings(
                GameLaunchSettingsResolver.Resolve(Current.Config, effectiveConfig),
                _independentConfig.UseGlobalGameSettings);
        }
        finally
        {
            _isLoadingSettings = false;
        }
    }

    private void UseGlobalGameSettingsToggle_OnIsCheckedChanged(object? sender, RoutedEventArgs e)
    {
        if (_isLoadingSettings)
        {
            return;
        }

        _independentConfig.UseGlobalGameSettings = UseGlobalGameSettingsToggle.IsChecked ?? true;
        LoadLaunchSettings();
        QueueConfigWrite();
    }

    private void SettingsEditor_OnSettingsChanged(object? sender, GameSettingsChangedEventArgs e)
    {
        if (_isLoadingSettings || _version == null || _independentConfig.UseGlobalGameSettings)
        {
            return;
        }

        var settings = e.Settings;
        switch (e.Field)
        {
            case GameSettingsField.AutoSelectJava:
                EnsureJavaConfig().AutoSelectJava = settings.AutoSelectJava;
                break;
            case GameSettingsField.SelectedJavaPath:
                EnsureJavaConfig().SelectedJavaPath = settings.SelectedJavaPath;
                break;
            case GameSettingsField.AutoAllocateMemory:
                EnsureJavaConfig().AutoAllocateMemory = settings.AutoAllocateMemory;
                break;
            case GameSettingsField.MaxMemoryMb:
                EnsureJavaConfig().MaxMemoryMb = settings.MaxMemoryMb;
                break;
            case GameSettingsField.JvmArguments:
                EnsureJavaConfig().JvmArguments = settings.JvmArguments;
                break;
            case GameSettingsField.WrapperArguments:
                EnsureJavaConfig().WrapperArguments = settings.WrapperArguments;
                break;
            case GameSettingsField.GameArguments:
                (_independentConfig.Game ??= new GameVersionGameConfig()).GameArguments = settings.GameArguments;
                break;
        }

        QueueConfigWrite();
    }

    private void OnInvalidJavaPathsFound(IReadOnlyList<string> paths)
    {
        if (!_independentConfig.UseGlobalGameSettings &&
            _independentConfig.Java?.SelectedJavaPath is { } selected &&
            paths.Any(path => string.Equals(path, selected, StringComparison.OrdinalIgnoreCase)))
        {
            EnsureJavaConfig().SelectedJavaPath = null;
            QueueConfigWrite();
        }

        LoadLaunchSettings();
    }

    private GameVersionJavaConfig EnsureJavaConfig() => _independentConfig.Java ??= new GameVersionJavaConfig();

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
