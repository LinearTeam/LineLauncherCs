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
