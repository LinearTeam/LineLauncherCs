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

using LMCUI.Pages.SettingsPage.LauncherSettings;

namespace LMCUI.Pages.SettingsPage;

using Avalonia.Interactivity;
using GameSettings;

public partial class SettingsPage : PageBase
{
    public SettingsPage() : base("Pages.SettingsPage.Title", "SettingsPage")
    {
        InitializeComponent();
    }
    void GameSettingsExpander_OnClick(object? sender, RoutedEventArgs e) {
        MainWindow.NavigatePage(new PageNavigateWay(typeof(GameSettingsPage), 
            MainWindow.Instance.mnv.SettingsItem), NavigateType.Append);
    }
    private void LauncherSettingsExpander_OnClick(object? sender, RoutedEventArgs e)
    {
        MainWindow.NavigatePage(new PageNavigateWay(typeof(LauncherSettingsPage), 
            MainWindow.Instance.mnv.SettingsItem), NavigateType.Append);
    }
    private void AboutExpander_OnClick(object? sender, RoutedEventArgs e)
    {
        MainWindow.NavigatePage(new PageNavigateWay(typeof(AboutPage), 
            MainWindow.Instance.mnv.SettingsItem), NavigateType.Append);
    }
}

