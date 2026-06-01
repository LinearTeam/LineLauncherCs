using LMCUI.Navigation;

namespace LineLauncherCs.Tests;

public class NavigationSupportTests
{
    [Fact]
    public void MainWindowNavigationMap_CreateDefaultContainsExpectedPages()
    {
        var map = MainWindowNavigationMap.CreateDefault();

        Assert.Equal("LMCUI.Pages.LaunchPage.LaunchPage", map["LaunchPage"].FullName);
        Assert.Equal("LMCUI.Pages.SettingsPage.SettingsPage", map["SettingsPage"].FullName);
        Assert.Equal(7, map.Count);
    }
}
