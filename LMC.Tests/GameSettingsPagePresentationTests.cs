using Avalonia.Media;
using LMCCore.Java;
using LMCUI.Pages.SettingsPage.GameSettings;

namespace LMC.Tests;

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
