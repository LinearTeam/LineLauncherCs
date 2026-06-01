using System;
using System.Collections.Generic;
using LMCUI.Pages.AccountPage;
using LMCUI.Pages.DownloadMinecraftPage;
using LMCUI.Pages.Help;
using LMCUI.Pages.LaunchPage;
using LMCUI.Pages.SettingsPage;
using LMCUI.Pages.TaskPage;
using LMCUI.Pages.VersionManagePage;

namespace LMCUI.Navigation;

internal static class MainWindowNavigationMap
{
    public static Dictionary<string, Type> CreateDefault()
    {
        return new Dictionary<string, Type>
        {
            ["LaunchPage"] = typeof(LaunchPage),
            ["VersionManagePage"] = typeof(VersionManagePage),
            ["DownloadMinecraftPage"] = typeof(DownloadMinecraftPage),
            ["AccountPage"] = typeof(AccountPage),
            ["TaskPage"] = typeof(TaskPage),
            ["SettingsPage"] = typeof(SettingsPage),
            ["HelpPage"] = typeof(HelpPage)
        };
    }
}
