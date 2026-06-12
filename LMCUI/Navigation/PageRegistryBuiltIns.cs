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
using LMCUI.Navigation.Model;
using LMCUI.Pages.SettingsPage.About;

namespace LMCUI.Navigation;

internal static class PageRegistryBuiltIns
{
    public static void RegisterAll(PageRegistry registry)
    {
        registry.Register<Pages.LaunchPage.LaunchPage>("LaunchPage");
        registry.Register<Pages.VersionManagePage.VersionManagePage>("VersionManagePage");
        registry.Register<Pages.DownloadMinecraftPage.DownloadMinecraftPage>("DownloadMinecraftPage");
        registry.Register(typeof(Pages.VersionManagePage.VersionDetailPage), "VersionDetailPage", PageStorageMode.Parameterized);
        registry.Register<Pages.AccountPage.AccountPage>("AccountPage");
        registry.Register<Pages.TaskPage.TaskPage>("TaskPage");
        registry.Register<Pages.SettingsPage.SettingsPage>("SettingsPage");
        registry.Register<Pages.Help.HelpPage>("HelpPage");
        registry.RegisterDynamic(typeof(Pages.Help.HelpContentPage), "HelpPage");
        registry.Register<Pages.SettingsPage.LauncherSettings.LauncherSettingsPage>("LauncherSettingsPage");
        registry.Register<Pages.SettingsPage.GameSettings.GameSettingsPage>("GameSettingsPage");
        registry.Register<AboutPage>("AboutPage");
        registry.Register<Pages.SettingsPage.About.CopyrightPage>("CopyrightPage");
    }
}
