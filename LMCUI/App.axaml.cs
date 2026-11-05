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

using System.Linq;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Styling;
using Avalonia.Threading;
using FluentAvalonia.Styling;

namespace LMCUI;

using I18n;
using LMC.Basic.Configs;
using LMCCore.Tasks;
using LMC.LifeCycle;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
        Startup.Initialize();
        ApplyThemePreference(LMC.Current.Config.ThemePreference);
        Dispatcher.UIThread.UnhandledException += OnUiThreadUnhandledException;
        I18nManager.Instance.LoadAllLanguages();
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow();
        }
        
        TaskManager.Instance.Start();
        base.OnFrameworkInitializationCompleted();
    }

    private static void OnUiThreadUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        CrashReportManager.TryWriteReport(e.Exception, "Dispatcher.UIThread.UnhandledException", true);
    }

    internal static ThemeVariant GetThemeVariant(LauncherThemePreference preference)
    {
        return preference switch
        {
            LauncherThemePreference.Light => ThemeVariant.Light,
            LauncherThemePreference.Dark => ThemeVariant.Dark,
            _ => ThemeVariant.Default
        };
    }

    internal void ApplyThemePreference(LauncherThemePreference preference)
    {
        var variant = GetThemeVariant(preference);
        foreach (var theme in Styles.OfType<FluentAvaloniaTheme>())
        {
            // Prevent later system color notifications from overriding a manually selected theme.
            theme.PreferSystemTheme = variant == ThemeVariant.Default;
        }

        RequestedThemeVariant = variant;
    }
}
