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
using Avalonia.Controls;
using LMC.LifeCycle;
using LMCUI.Controls;

namespace LMCUI.Pages.SettingsPage.LauncherSettings;

using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Interactivity;
using FluentAvalonia.UI.Controls;
using I18n;
using LMC;
using LMC.Basic.Configs;
using LMC.Basic.Logging;
using Utils;

public partial class LauncherSettingsPage : PageBase {
    private readonly Logger _logger = new Logger("LauncherSettingsPage");
    private bool _isInitializing;
    
    public LauncherSettingsPage() : base(I18nManager.Instance.GetString("Pages.SettingsPage.LauncherSettingsPage.Title"), "LauncherSettingsPage") {
        InitializeComponent();
        Loaded += (_, _) => LoadConfigs();
    }

    private void LoadConfigs()
    {
        _isInitializing = true;
        try
        {
            var languages = I18nManager.Instance.AvailableCultures;
            LanguageComboBox.ItemsSource = new ObservableCollection<string>(languages.Select(culture => culture.NativeName));
            LanguageComboBox.SelectedItem = I18nManager.Instance.CurrentCulture.NativeName;
            var themes = new[]
            {
                new ThemeOption(LauncherThemePreference.System,
                    I18nManager.Instance.GetString("Pages.SettingsPage.LauncherSettingsPage.AppearanceSettings.ThemeExpander.System")),
                new ThemeOption(LauncherThemePreference.Light,
                    I18nManager.Instance.GetString("Pages.SettingsPage.LauncherSettingsPage.AppearanceSettings.ThemeExpander.Light")),
                new ThemeOption(LauncherThemePreference.Dark,
                    I18nManager.Instance.GetString("Pages.SettingsPage.LauncherSettingsPage.AppearanceSettings.ThemeExpander.Dark"))
            };
            ThemeComboBox.ItemsSource = themes;
            ThemeComboBox.SelectedItem = themes.FirstOrDefault(option =>
                option.Preference == Current.Config.ThemePreference) ?? themes[0];
        }
        finally
        {
            _isInitializing = false;
        }
    }

    private void ThemeComboBox_OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_isInitializing || ThemeComboBox.SelectedItem is not ThemeOption option)
        {
            return;
        }

        Current.Config.ThemePreference = option.Preference;
        if (Avalonia.Application.Current is App app)
        {
            app.ApplyThemePreference(option.Preference);
        }

        _ = SaveConfigAsync();
    }

    private async Task SaveConfigAsync()
    {
        try
        {
            await Task.Run(Current.SaveConfig);
        }
        catch (Exception ex)
        {
            _logger.Warn($"保存启动器外观设置失败: {ex}");
        }
    }

    private void OpenTranslatePage(object? sender, RoutedEventArgs e)
    {
        CrossPlatformUtils.OpenUrl("https://crowdin.com/project/linelaunchercs");
    }
    private void LanguageComboBox_OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_isInitializing) return;
        var selectedLanguage = LanguageComboBox.SelectedItem as string;
        if (selectedLanguage == null) return;
        var culture = I18nManager.Instance.AvailableCultures.FirstOrDefault(c => c.NativeName == selectedLanguage);
        if(culture == null) return;
        Current.Config.SelectedLanguage = culture.Name;
        _ = SaveConfigAsync();
        if (!culture.Equals(I18nManager.Instance.CurrentCulture))
        {
            var restartTeachingTip = new FATeachingTip
            {
                Title = I18nManager.Instance.GetString("Pages.SettingsPage.LauncherSettingsPage.AppearanceSettings.LanguageExpander.RestartTeachingTip.Title"),
                Subtitle = I18nManager.Instance.GetString("Pages.SettingsPage.LauncherSettingsPage.AppearanceSettings.LanguageExpander.RestartTeachingTip.Subtitle"),
                CloseButtonContent = I18nManager.Instance.GetString("Pages.SettingsPage.LauncherSettingsPage.AppearanceSettings.LanguageExpander.RestartTeachingTip.CloseButtonText"),
                ActionButtonContent = I18nManager.Instance.GetString("Pages.SettingsPage.LauncherSettingsPage.AppearanceSettings.LanguageExpander.RestartTeachingTip.RestartButtonText"),
            };
            restartTeachingTip.ActionButtonClick += (_, _) =>
            {
                Restart.Run();
            };
            MessageQueueControl.Instance.AddTeachingTip(restartTeachingTip, 10 * 1000);
        }
    }

    private sealed record ThemeOption(LauncherThemePreference Preference, string Name)
    {
        public override string ToString() => Name;
    }
}
