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
using Avalonia.Media;
using LMCCore.Java;
using LMCUI.Pages.SettingsPage.GameSettings;

namespace LineLauncherCs.Tests;

public class GameSettingsPagePresentationTests
{
    [Fact]
    public void BuildJavaItemViewData_MarksSelectedItemAndBuildsHeader()
    {
        var items = GameSettingsPagePresentation.BuildJavaItemViewData(
            [
                new LocalJava
                {
                    Path = "C:\\Java\\jdk-17",
                    Version = new Version(17, 0, 9),
                    Implementor = "Eclipse Adoptium",
                    IsJdk = true
                }
            ],
            "C:\\Java\\jdk-17",
            "已启用");

        var item = Assert.Single(items);
        Assert.True(item.IsSelected);
        Assert.Contains("JDK-17.0.9", item.Header);
        Assert.Contains("已启用", item.Header);
    }

    [Fact]
    public void BuildJavaItems_AssignsSelectedAndDefaultBrushes()
    {
        var selectedBrush = Brushes.LawnGreen;
        var defaultBrush = Brushes.White;

        var items = GameSettingsPagePresentation.BuildJavaItems(
            [
                new JavaItemViewData("a", "A", true),
                new JavaItemViewData("b", "B", false)
            ],
            selectedBrush,
            defaultBrush);

        Assert.Same(selectedBrush, items[0].Foreground);
        Assert.Same(defaultBrush, items[1].Foreground);
    }

    [Fact]
    public void ResolveJavaRootPath_TrimsBinDirectory()
    {
        var root = GameSettingsPagePresentation.ResolveJavaRootPath("C:\\Java\\jdk-21\\bin\\java.exe");

        Assert.Equal("C:\\Java\\jdk-21", root);
    }

    [Fact]
    public void GetJavaListHeaderKey_UsesEmptyHeaderWhenNoItems()
    {
        Assert.Equal(
            "Pages.SettingsPage.GameSettingsPage.JavaRuntime.JavaListExpander.EmptyHeader",
            GameSettingsPagePresentation.GetJavaListHeaderKey(0));
        Assert.Equal(
            "Pages.SettingsPage.GameSettingsPage.JavaRuntime.JavaListExpander.Header",
            GameSettingsPagePresentation.GetJavaListHeaderKey(2));
    }
}
