using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Avalonia.Media;
using LMCCore.Java;

namespace LMCUI.Pages.SettingsPage.GameSettings;

internal sealed record JavaItemViewData(
    string Path,
    string Header,
    bool IsSelected);

internal static class GameSettingsPagePresentation
{
    public static IReadOnlyList<JavaItemViewData> BuildJavaItemViewData(
        IEnumerable<LocalJava> javas,
        string selectedJavaPath,
        string enabledText)
    {
        return javas
            .Select(java => new JavaItemViewData(
                java.Path,
                BuildJavaHeader(java, selectedJavaPath, enabledText),
                string.Equals(java.Path, selectedJavaPath, StringComparison.OrdinalIgnoreCase)))
            .ToList();
    }

    public static IReadOnlyList<JavaItem> BuildJavaItems(
        IEnumerable<JavaItemViewData> items,
        IBrush selectedBrush,
        IBrush defaultBrush)
    {
        return items
            .Select(item => new JavaItem
            {
                Path = item.Path,
                Header = item.Header,
                IsSelected = item.IsSelected,
                Foreground = item.IsSelected ? selectedBrush : defaultBrush
            })
            .ToList();
    }

    public static string GetJavaListHeaderKey(int count)
    {
        return count == 0
            ? "Pages.SettingsPage.GameSettingsPage.JavaRuntime.JavaListExpander.EmptyHeader"
            : "Pages.SettingsPage.GameSettingsPage.JavaRuntime.JavaListExpander.Header";
    }

    public static string ResolveJavaRootPath(string selectedFilePath)
    {
        var root = Path.GetDirectoryName(selectedFilePath)
                   ?? throw new InvalidOperationException("Selected file path has no directory");
        if (root.EndsWith("bin", StringComparison.OrdinalIgnoreCase))
        {
            root = Path.GetDirectoryName(root)
                   ?? throw new InvalidOperationException("Java bin directory has no parent");
        }

        return root;
    }

    private static string BuildJavaHeader(LocalJava java, string selectedJavaPath, string enabledText)
    {
        var selectedSuffix = string.Equals(java.Path, selectedJavaPath, StringComparison.OrdinalIgnoreCase)
            ? $"({enabledText})"
            : string.Empty;
        return $"{(java.IsJdk ? "JDK" : "JRE")}-{java.Version} {java.Implementor} {selectedSuffix}".Trim();
    }
}
