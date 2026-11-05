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
using System.Threading.Tasks;
using Avalonia.Threading;
using LMC;
using LMC.Basic.Configs;
using LMC.Basic.Logging;
using LMCCore.Game.Launching.Configuration;
using LMCUI.Controls.GameSettings;
using LMCUI.I18n;

namespace LMCUI.Pages.SettingsPage.GameSettings;

public partial class GameSettingsPage : PageBase
{
    private readonly Logger _logger = new("GameSettingsPage");
    private readonly DispatcherTimer _launchSettingsSaveTimer;
    private bool _hasPendingLaunchSettingsSave;
    private bool _isSavingLaunchSettings;

    public GameSettingsPage() : base(
        I18nManager.Instance.GetString("Pages.SettingsPage.GameSettingsPage.Title"), "GameSettingsPage")
    {
        InitializeComponent();
        _launchSettingsSaveTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(400) };
        _launchSettingsSaveTimer.Tick += LaunchSettingsSaveTimer_OnTick;
        SettingsEditor.InvalidJavaPathsFound += OnInvalidJavaPathsFound;
        Loaded += (_, _) => LoadSettings();
        Unloaded += (_, _) => FlushLaunchSettingsSave();
    }

    private void LoadSettings()
    {
        SettingsEditor.LoadSettings(GameLaunchSettingsResolver.Resolve(Current.Config, null));
    }

    private void OnInvalidJavaPathsFound(IReadOnlyList<string> paths) => LoadSettings();

    private void SettingsEditor_OnSettingsChanged(object? sender, GameSettingsChangedEventArgs e)
    {
        var config = Current.Config.GameLaunch ??= new GameLaunchConfig();
        var settings = e.Settings;
        switch (e.Field)
        {
            case GameSettingsField.AutoSelectJava:
                Current.Config.AutoSelectJava = settings.AutoSelectJava;
                break;
            case GameSettingsField.SelectedJavaPath:
                Current.Config.SelectedJavaPath = settings.SelectedJavaPath;
                break;
            case GameSettingsField.AutoAllocateMemory:
                config.AutoAllocateMemory = settings.AutoAllocateMemory;
                break;
            case GameSettingsField.MaxMemoryMb:
                config.MaxMemoryMb = settings.MaxMemoryMb;
                break;
            case GameSettingsField.JvmArguments:
                config.JvmArguments = settings.JvmArguments;
                break;
            case GameSettingsField.WrapperArguments:
                config.WrapperArguments = settings.WrapperArguments;
                break;
            case GameSettingsField.GameArguments:
                config.GameArguments = settings.GameArguments;
                break;
        }

        ScheduleLaunchSettingsSave();
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
}
