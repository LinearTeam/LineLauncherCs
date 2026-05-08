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

namespace LMCUI.Pages.SettingsPage;

using LMC;
using LMCUI.I18n;

public partial class AboutPage : PageBase
{
    public AboutPage() : base("Pages.AboutPage.Title", "AboutPage")
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var versionFormat = I18nManager.Instance.GetString("Pages.AboutPage.AboutLauncherExpander.VersionDescription");
        AboutLauncherExpander.Description = string.Format(versionFormat, Current.VersionType, Current.Version, Current.BuildNumber);
    }
}