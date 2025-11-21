using Avalonia;
using Avalonia.Controls;
using LMC.LifeCycle;
using LMCUI.Controls;

namespace LMCUI.Pages.SettingsPage.LauncherSettings;

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

public partial class LauncherSettingsPage : PageBase {
    private readonly Logger _logger = new Logger("LauncherSettingsPage");
    
    public LauncherSettingsPage() : base(I18nManager.Instance.GetString("Pages.SettingsPage.LauncherSettingsPage.Title"), "LauncherSettingsPage") {
        InitializeComponent();
        Loaded += ((sender, args) => {
            _ = Task.Run(async () => await OnLoaded(sender, args));
        });
    }
    async Task OnLoaded(object? sender, RoutedEventArgs e) {
        await LoadConfigs();
    }

    Task LoadConfigs()
    {
        Dispatcher.UIThread.Invoke(() =>
        {
            var languages = I18nManager.Instance.AvailableCultures;
            LanguageComboBox.ItemsSource = new ObservableCollection<string>(languages.Select(culture => culture.NativeName));
            LanguageComboBox.SelectedItem = I18nManager.Instance.CurrentCulture.NativeName;
        });
        return Task.CompletedTask;
    }

    private void OpenTranslatePage(object? sender, RoutedEventArgs e)
    {
        CrossPlatformUtils.OpenUrl("https://crowdin.com/project/linelaunchercs");
    }
    private void LanguageComboBox_OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        var selectedLanguage = LanguageComboBox.SelectedItem as string;
        if (selectedLanguage == null) return;
        var culture = I18nManager.Instance.AvailableCultures.FirstOrDefault(c => c.NativeName == selectedLanguage);
        if(culture == null) return;
        Current.Config.SelectedLanguage = culture.Name;
        Current.SaveConfig();
        if (!culture.Equals(I18nManager.Instance.CurrentCulture))
        {
            var restartTeachingTip = new TeachingTip
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
}
