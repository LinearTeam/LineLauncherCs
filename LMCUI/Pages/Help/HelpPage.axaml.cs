using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using FluentAvalonia.UI.Controls;
using LMC.Basic.Logging;
using LMC.Help.Models;
using LMCCore.Utils;
using LMCUI.Utils;
using Markdown.Avalonia;

namespace LMCUI.Pages.Help;

public partial class HelpPage : HelpContentPage
{
    private HelpFile? _helpFile;
    private readonly static Logger s_logger = new("HelpPage");

    public HelpPage() : base(new HelpContentPageParam("帮助中心", "HelpPage", []))
    {
        this.Loaded += OnLoaded;
        InitializeComponent();
    }
    async private void OnLoaded(object? sender, RoutedEventArgs e)
    {
        await LoadHelpFile(Path.GetFullPath(Path.Combine("Assets","help.yaml")));
        LoadUi();
    }

    public async Task LoadHelpFile(string path)
    {
        try
        {
            _helpFile = await LMC.Help.HelpParser.ParseYamlFile(path);
            s_logger.Info("帮助文件已加载。");
        }
        catch (Exception ex)
        {
            s_logger.Error(ex, "Loading help file");
            _ = MessageQueueHelper.ShowTeachingTip("帮助文件加载失败，若持续出现请于 设置 - 关于 中的反馈渠道反馈问题。", "错误信息：" + ex.Message, 15000);
        }
    }

    public void LoadUi()
    {
        try
        {
            this.HelpItems = _helpFile!.Helps;
            OnLoaded();
        }
        catch(Exception ex)
        {
            s_logger.Error(ex, "Loading help UI");
            _ = MessageQueueHelper.ShowTeachingTip("帮助文件加载失败，若持续出现请于 设置 - 关于 中的反馈渠道反馈问题。", "错误信息：" + ex.Message, 15000);
        }
    }

}

public class HelpContentPage : PageBase
{
    protected List<BaseHelpItem> HelpItems;
    public HelpContentPage(HelpContentPageParam hcpp) : base(hcpp.Title, hcpp.Tag)
    {
        HelpItems = hcpp.HelpItems;
        this.Loaded += OnLoaded;
    }
    public HelpContentPage() : base("",""){}
    public void OnLoaded()
    {
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
        foreach (var helpItem in HelpItems)
        {
            switch (helpItem)
            {
                case MarkdownHelpItem mhi:
                {
                    var settingsExpander = new SettingsExpander
                    {
                        Header = helpItem.Title,
                        IsExpanded = false,
                        Margin = new Thickness(0, 0, 0, 10),
                        Description = mhi.Description,
                    };
                    settingsExpander.Items.Add(new SettingsExpanderItem
                    {
                        Content = new MarkdownScrollViewer
                        {
                            Markdown = mhi.Text,
                            Margin = new Thickness(3),
                        }
                    });
                    settingsExpander.IconSource = mhi.Icon.Type switch
                    {
                        IconType.BuiltIn => new SymbolIconSource
                        {
                            Symbol = (Symbol)Enum.Parse(typeof(Symbol), mhi.Icon.Content ?? "Help")
                        },
                        IconType.Url => new BitmapIconSource
                        {
                            UriSource = new Uri(mhi.Icon.Content ?? string.Empty)
                        },
                        IconType.Assets => new BitmapIconSource
                        {
                            UriSource = new Uri("avares://LMCUI/" + (mhi.Icon.Content ?? string.Empty))
                        },
                        _ => settingsExpander.IconSource
                    };
                    stackPanel.Children.Add(settingsExpander);
                    break;
                }

                case SectionHelpItem shi:
                {
                    var settingsCard = new SettingsExpander
                    {
                        IsClickEnabled = true,
                        ActionIconSource = new SymbolIconSource
                        {
                            Symbol = Symbol.ChevronRight,
                        },
                        Header = shi.Title,
                        Description = shi.Description,
                    };
                    settingsCard.IconSource = shi.Icon.Type switch
                    {
                        IconType.BuiltIn => new SymbolIconSource
                        {
                            Symbol = (Symbol)Enum.Parse(typeof(Symbol), shi.Icon.Content ?? "Help")
                        },
                        IconType.Url => new BitmapIconSource
                        {
                            UriSource = new Uri(shi.Icon.Content ?? string.Empty)
                        },
                        IconType.Assets => new BitmapIconSource
                        {
                            UriSource = new Uri("avares://LMCUI/" + (shi.Icon.Content ?? string.Empty))
                        },
                        _ => settingsCard.IconSource
                    };
                    settingsCard.Click += (_, _) =>
                    {
                        var param = new HelpContentPageParam(shi.Title, this.Tag + "." + shi.Key, shi.Helps);
                        MainWindow.NavigatePage(
                            new PageNavigateWay(
                                typeof(HelpContentPage), 
                                param,
                                (NavigationViewItem) MainWindow.Instance.mnv.SelectedItem,
                                directlySet: true),
                            NavigateType.Append);
                    };
                    stackPanel.Children.Add(settingsCard);
                    break;
                }
                
            }
        }
        this.Content = scrollViewer;
    }
    private void OnLoaded(object? sender, RoutedEventArgs e)
    {
        OnLoaded();
    }

    public override void ProcessParameter(object? param)
    {
        if (param is HelpContentPageParam hcpp)
        {
            this.Title = hcpp.Title;
            this.Tag  = hcpp.Tag;
            HelpItems = hcpp.HelpItems;
            this.Loaded += OnLoaded;
        }
    }
}


public record HelpContentPageParam(string Title, string Tag, List<BaseHelpItem> HelpItems);