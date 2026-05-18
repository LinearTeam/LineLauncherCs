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

using System;
using System.Collections.Generic;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using FluentAvalonia.UI.Controls;
using LMCUI.I18n;

namespace LMCUI.Pages.SettingsPage;

public partial class CopyrightPage : PageBase
{
    public CopyrightPage() : base("Pages.CopyrightPage.Title", "CopyrightPage")
    {
        InitializeComponent();
        I18nManager.Instance.CultureChanged += LoadCopyrightText;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnLoaded(object? sender, RoutedEventArgs e)
    {
        LoadCopyrightText();
    }

    private void OnUnloaded(object? sender, RoutedEventArgs e)
    {
        I18nManager.Instance.CultureChanged -= LoadCopyrightText;
    }

    public void LoadCopyrightText()
    {
        CopyrightItems.Children.Clear();
        foreach (var group in CreateCopyrightGroups())
        {
            CopyrightItems.Children.Add(CreateGroupExpander(group));
        }
    }

    private static IEnumerable<CopyrightGroupModel> CreateCopyrightGroups()
    {
        yield return new CopyrightGroupModel(
            "Pages.CopyrightPage.Items.SystemGroup.Title",
            "Pages.CopyrightPage.Items.SystemGroup.Description",
            new[]
            {
                new CopyrightItemModel(
                    "Pages.CopyrightPage.Items.System.Title",
                    "Pages.CopyrightPage.Items.System.Description")
            });

        yield return new CopyrightGroupModel(
            "Pages.CopyrightPage.Items.AvaloniaGroup.Title",
            "Pages.CopyrightPage.Items.AvaloniaGroup.Description",
            new[]
            {
                new CopyrightItemModel("Pages.CopyrightPage.Items.Avalonia.Title", "Pages.CopyrightPage.Items.Avalonia.Description"),
                new CopyrightItemModel("Pages.CopyrightPage.Items.AvaloniaDesktop.Title", "Pages.CopyrightPage.Items.AvaloniaDesktop.Description"),
                new CopyrightItemModel("Pages.CopyrightPage.Items.AvaloniaThemesFluent.Title", "Pages.CopyrightPage.Items.AvaloniaThemesFluent.Description"),
                new CopyrightItemModel("Pages.CopyrightPage.Items.AvaloniaFontsInter.Title", "Pages.CopyrightPage.Items.AvaloniaFontsInter.Description"),
                new CopyrightItemModel("Pages.CopyrightPage.Items.AvaloniaDiagnostics.Title", "Pages.CopyrightPage.Items.AvaloniaDiagnostics.Description")
            });

        yield return new CopyrightGroupModel(
            "Pages.CopyrightPage.Items.FluentAvaloniaGroup.Title",
            "Pages.CopyrightPage.Items.FluentAvaloniaGroup.Description",
            new[]
            {
                new CopyrightItemModel("Pages.CopyrightPage.Items.FluentAvalonia.Title", "Pages.CopyrightPage.Items.FluentAvalonia.Description")
            });

        yield return new CopyrightGroupModel(
            "Pages.CopyrightPage.Items.OtherGroup.Title",
            "Pages.CopyrightPage.Items.OtherGroup.Description",
            new[]
            {
                new CopyrightItemModel("Pages.CopyrightPage.Items.NLog.Title", "Pages.CopyrightPage.Items.NLog.Description"),
                new CopyrightItemModel("Pages.CopyrightPage.Items.MicrosoftNetTestSdk.Title", "Pages.CopyrightPage.Items.MicrosoftNetTestSdk.Description"),
                new CopyrightItemModel("Pages.CopyrightPage.Items.Xunit.Title", "Pages.CopyrightPage.Items.Xunit.Description"),
                new CopyrightItemModel("Pages.CopyrightPage.Items.XunitRunnerVisualStudio.Title", "Pages.CopyrightPage.Items.XunitRunnerVisualStudio.Description"),
                new CopyrightItemModel("Pages.CopyrightPage.Items.CoverletCollector.Title", "Pages.CopyrightPage.Items.CoverletCollector.Description"),
                new CopyrightItemModel("Pages.CopyrightPage.Items.Moq.Title", "Pages.CopyrightPage.Items.Moq.Description")
            });

        yield return new CopyrightGroupModel(
            "Pages.CopyrightPage.Items.SpecialThanksGroup.Title",
            "Pages.CopyrightPage.Items.SpecialThanksGroup.Description",
            new[]
            {
                new CopyrightItemModel("Pages.CopyrightPage.Items.PCL2HMCL.Title", "Pages.CopyrightPage.Items.PCL2HMCL.Description"),
                new CopyrightItemModel("Pages.CopyrightPage.Items.TT702ForgeInstallProcessor.Title", "Pages.CopyrightPage.Items.TT702ForgeInstallProcessor.Description"),
                new CopyrightItemModel("Pages.CopyrightPage.Items.BMCLAPI.Title", "Pages.CopyrightPage.Items.BMCLAPI.Description"),
                new CopyrightItemModel("Pages.CopyrightPage.Items.MinecraftWiki.Title", "Pages.CopyrightPage.Items.MinecraftWiki.Description"),
                new CopyrightItemModel("Pages.CopyrightPage.Items.FluentLauncher.Title", "Pages.CopyrightPage.Items.FluentLauncher.Description")
            });
    }

    private static FASettingsExpander CreateGroupExpander(CopyrightGroupModel group)
    {
        var expander = new FASettingsExpander
        {
            IsExpanded = false,
            Header = I18nManager.Instance.GetString(group.TitleKey),
            Description = I18nManager.Instance.GetString(group.DescriptionKey),
            IconSource = new FASymbolIconSource { Symbol = FASymbol.Document }
        };

        var container = new StackPanel { Orientation = Orientation.Vertical, Spacing = 6 };
        foreach (var item in group.Items)
        {
            container.Children.Add(CreateItemExpander(item));
        }

        expander.Items.Add(new FASettingsExpanderItem { Content = container });

        return expander;
    }

    private static FASettingsExpander CreateItemExpander(CopyrightItemModel item)
    {
        return new FASettingsExpander
        {
            IsExpanded = false,
            Header = I18nManager.Instance.GetString(item.TitleKey),
            Description = I18nManager.Instance.GetString(item.DescriptionKey),
            IconSource = new FASymbolIconSource { Symbol = FASymbol.Document }
        };
    }

    private sealed record CopyrightGroupModel(string TitleKey, string DescriptionKey, IReadOnlyList<CopyrightItemModel> Items);
    private sealed record CopyrightItemModel(string TitleKey, string DescriptionKey);
}
