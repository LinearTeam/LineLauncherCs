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
using LMCUI.Navigation;
using LMCUI.Pages;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using FluentAvalonia.UI.Controls;
using LMCUI;

namespace LineLauncherCs.Tests;

public class NavigationSupportTests
{
    [Fact]
    public Task EscapeNavigation_DefersToDialogsAndOpenPopups() => TestAppBuilder.RunOnUiThreadAsync(() =>
    {
        var root = new StackPanel();
        Assert.False(MainWindow.HasEscapeNavigationBlocker(root));
        Assert.True(MainWindow.HasEscapeNavigationBlocker(root, new FAContentDialog()));
        foreach (var blocker in new Control[]
                 {
                     new FAContentDialog(),
                     new FADialogHost { Content = new FAContentDialog() },
                     new ComboBox { IsDropDownOpen = true },
                     new Popup { IsOpen = true }
                 })
        {
            root.Children.Add(blocker);
            Assert.True(MainWindow.HasEscapeNavigationBlocker(root), blocker.GetType().Name);
            root.Children.Remove(blocker);
        }

        root.Children.Add(new ComboBox { IsDropDownOpen = false });
        root.Children.Add(new Popup { IsOpen = false });
        root.Children.Add(new TextBox());
        root.Children.Add(new FAContentDialog { IsVisible = false });
        Assert.False(MainWindow.HasEscapeNavigationBlocker(root));
    });

    [Fact]
    public Task Navigate_ToSameCachedPage_ReopensWithoutDuplicatingHistory() => TestAppBuilder.RunOnUiThreadAsync(() =>
    {
        var page = new TestPage();
        var frame = new LineFrame { IsAnimationEnabled = false };
        var registration = new PageRegistration
        {
            PageType = typeof(TestPage),
            Factory = _ => page
        };
        var navigated = 0;
        frame.Navigated += (_, _) => navigated++;
        Assert.True(frame.Navigate(registration));

        var contentChanges = new List<object?>();
        frame.PropertyChanged += (_, e) =>
        {
            if (e.Property == LineFrame.ContentProperty)
                contentChanges.Add(frame.Content);
        };

        Assert.True(frame.Navigate(registration));

        Assert.Equal(2, navigated);
        Assert.Collection(contentChanges, value => Assert.Null(value), value => Assert.Same(page, value));
        Assert.Same(page, frame.CurrentPage);
        Assert.Equal(0, frame.HistoryCount);
    });

    private sealed class TestPage : PageBase
    {
    }

    [Fact]
    public void MainWindowNavigationMap_CreateDefaultContainsExpectedPages()
    {
        var map = MainWindowNavigationMap.CreateDefault();

        Assert.Equal("LMCUI.Pages.LaunchPage.LaunchPage", map["LaunchPage"].FullName);
        Assert.Equal("LMCUI.Pages.SettingsPage.SettingsPage", map["SettingsPage"].FullName);
        Assert.Equal(map["DownloadMinecraftPage"], map["DownloadPage"]);
    }
}
