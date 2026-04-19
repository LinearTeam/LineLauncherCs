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

using Avalonia.Interactivity;
using FluentAvalonia.UI.Controls;

namespace LMCUI.Pages.VersionManagePage;

public partial class VersionManagePage : PageBase
{
    public VersionManagePage() : base("Pages.VersionManagePage.Title","VersionManagePage")
    {
        InitializeComponent();
    }

    private void Button_OnClick(object? sender, RoutedEventArgs e)
    {
        MainWindow.NavigatePage(new PageNavigateWay(
            typeof(LaunchPage.LaunchPage),
            (NavigationViewItem)MainWindow.Instance.mnv.SelectedItem
            ), NavigateType.Append);
    }
}