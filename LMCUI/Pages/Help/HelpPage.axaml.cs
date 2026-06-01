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
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using FluentAvalonia.UI.Controls;
using LMC.Basic.Logging;
using LMC.Help.Models;
using LMCUI.I18n;
using LMCUI.Navigation;
using LMCUI.Utils;
using Markdown.Avalonia;

namespace LMCUI.Pages.Help;

public partial class HelpPage : HelpContentPage
{
    private readonly static Logger s_logger = new("HelpPage");
    private bool _isLoaded;

    public HelpPage() : base(new HelpContentPageParam(I18nManager.Instance.GetString("Pages.HelpPage.Title"), "HelpPage", []))
    {
        Loaded += OnLoaded;
        InitializeComponent();
    }

    async private void OnLoaded(object? sender, RoutedEventArgs e)
    {
        if (_isLoaded)
        {
            return;
        }

        _isLoaded = true;
        var helpFilePath = HelpPageSupport.GetHelpFilePath(AppContext.BaseDirectory);
        var loadResult = await HelpPageSupport.LoadHelpFileAsync(helpFilePath);
        if (!loadResult.Success)
        {
            var exception = loadResult.Exception!;
            s_logger.Error(exception, "Loading help file");
            ShowEmptyState(GetLoadFailedText());
            _ = MessageQueueHelper.ShowTeachingTip(
                I18nManager.Instance.GetString("Pages.HelpPage.LoadingFailedTip.Title"),
                I18nManager.Instance.GetString("Pages.HelpPage.LoadingFailedTip.Content") +
                I18nManager.Instance.GetString("Pages.HelpPage.LoadingFailedTip.ErrorPrefix") +
                exception.Message,
                15000);
            return;
        }

        s_logger.Info("帮助文件已加载。");
        SetHelpContent(new HelpContentPageParam(Title, Tag, loadResult.HelpFile.Helps));
    }

    private static string GetLoadFailedText()
    {
        return CultureInfo.CurrentUICulture.TwoLetterISOLanguageName.Equals("zh", StringComparison.OrdinalIgnoreCase)
            ? "帮助内容不可用"
            : "Help content unavailable.";
    }
}

public class HelpContentPage : PageBase
{
    protected IReadOnlyList<BaseHelpItem> HelpItems = [];

    public HelpContentPage(HelpContentPageParam hcpp) : base(hcpp.Title, hcpp.Tag)
    {
        SetHelpContent(hcpp);
    }

    public HelpContentPage() : base("", "")
    {
    }

    protected void SetHelpContent(HelpContentPageParam param)
    {
        Title = param.Title;
        Tag = param.Tag;
        HelpItems = param.HelpItems;
        RenderContent();
    }

    protected void ShowEmptyState(string text)
    {
        Content = new Grid
        {
            Margin = new Thickness(60, 30),
            Children =
            {
                new TextBlock
                {
                    Text = text,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    Opacity = 0.75
                }
            }
        };
    }

    public override void ProcessParameter(object? param)
    {
        if (param is HelpContentPageParam hcpp)
        {
            SetHelpContent(hcpp);
        }
    }

    private void RenderContent()
    {
        var renderItems = HelpPageSupport.BuildRenderItems(Tag, HelpItems);
        if (renderItems.Count == 0)
        {
            ShowEmptyState(GetEmptyContentText());
            return;
        }

        var scrollViewer = new ScrollViewer
        {
            Margin = new Thickness(60, 30),
        };
        var stackPanel = new StackPanel
        {
            Orientation = Orientation.Vertical,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Spacing = 0
        };
        scrollViewer.Content = stackPanel;
        foreach (var helpItem in renderItems)
        {
            switch (helpItem.Kind)
            {
                case HelpItemRenderKind.Markdown:
                {
                    var settingsExpander = new FASettingsExpander
                    {
                        Header = helpItem.Title,
                        IsExpanded = false,
                        Margin = new Thickness(0, 0, 0, 10),
                        Description = helpItem.Description,
                        IconSource = CreateIconSource(helpItem.Icon)
                    };
                    settingsExpander.Items.Add(new FASettingsExpanderItem
                    {
                        Content = new MarkdownScrollViewer
                        {
                            Markdown = helpItem.Markdown ?? string.Empty,
                            Margin = new Thickness(3),
                        }
                    });
                    stackPanel.Children.Add(settingsExpander);
                    break;
                }

                case HelpItemRenderKind.Section:
                {
                    var settingsCard = new FASettingsExpander
                    {
                        IsClickEnabled = true,
                        ActionIconSource = new FASymbolIconSource
                        {
                            Symbol = FASymbol.ChevronRight,
                        },
                        Header = helpItem.Title,
                        Description = helpItem.Description,
                        IconSource = CreateIconSource(helpItem.Icon)
                    };
                    settingsCard.Click += (_, _) =>
                    {
                        if (helpItem.NavigationTarget == null)
                        {
                            return;
                        }

                        MainWindow.NavigatePage(
                            new PageNavigateWay(
                                typeof(HelpContentPage),
                                helpItem.NavigationTarget,
                                (FANavigationViewItem)MainWindow.Instance.mnv.SelectedItem,
                                directlySet: true),
                            NavigateType.Append);
                    };
                    stackPanel.Children.Add(settingsCard);
                    break;
                }
            }
        }

        Content = scrollViewer;
    }

    private static FAIconSource? CreateIconSource(HelpIconRenderData icon)
    {
        return icon.Kind switch
        {
            HelpIconRenderKind.BuiltIn => new FASymbolIconSource
            {
                Symbol = (FASymbol)Enum.Parse(typeof(FASymbol), icon.Value ?? "Help")
            },
            HelpIconRenderKind.Url => new FABitmapIconSource
            {
                UriSource = new Uri(icon.Value ?? string.Empty)
            },
            HelpIconRenderKind.Assets => new FABitmapIconSource
            {
                UriSource = new Uri("avares://LMCUI/" + (icon.Value ?? string.Empty))
            },
            _ => null
        };
    }

    private static string GetEmptyContentText()
    {
        return CultureInfo.CurrentUICulture.TwoLetterISOLanguageName.Equals("zh", StringComparison.OrdinalIgnoreCase)
            ? "暂无帮助内容"
            : "No help content available.";
    }
}

public record HelpContentPageParam(string Title, string Tag, IReadOnlyList<BaseHelpItem> HelpItems);
