using System;
using System.Collections.ObjectModel;
using System.Linq;
using Avalonia.Interactivity;
using FluentAvalonia.UI.Controls;
using FluentAvalonia.UI.Windowing;
using LMC.Basic.Logging;
using LMCUI.Controls;
using LMCUI.Pages;
using LMCUI.Pages.AccountPage;
using LMCUI.Pages.Help;
using LMCUI.Pages.LaunchPage;
using LMCUI.Pages.SettingsPage;
using LMCUI.Pages.VersionManagePage;

namespace LMCUI;

using System.Globalization;
using I18n;

public partial class MainWindow : AppWindow
{
    private static bool s_isCodeChangeSelection;
    private static Logger s_logger = new Logger("MainWindow");
    private readonly static Type[] s_directNavigationPages = new[]
    {
        typeof(LaunchPage), 
        typeof(VersionManagePage),
        typeof(AccountPage),
        typeof(HelpPage), 
        typeof(SettingsPage),
    };
    public static MainWindow Instance { get; private set; } = null!;
    public ObservableCollection<BreadCrumbBarItem> BreadCrumbItemSource = new ObservableCollection<BreadCrumbBarItem>();
    public MainWindow()
    {
        // I18nManager.Instance.CurrentCulture = CultureInfo.GetCultureInfo("en-US");
        Instance = this;
        InitializeComponent();
        SplashScreen = new LineSplashScreen();
        Loaded += OnLoaded;
    }
    private void OnLoaded(object? sender, RoutedEventArgs e)
    {
        mnv.SettingsItem.Tag = "SettingsPage";
        mnv.SettingsItem.Content = I18nManager.Instance.GetString("MainWindow.NavItems.SettingsPage");
    }

    public static void NavigatePage(PageNavigateWay way, NavigateType type, string? tag = null, bool thrown = false)
    {
        NavigatePage(way.PageType, type, way.Item, way.Param, tag, thrown, way.DirectlySet);
    }
    
    private static void NavigatePage(Type page, NavigateType type, NavigationViewItem item, object? param = null, string? tag = null, bool thrown = false, bool directlySet = false)
    {
        if(type == NavigateType.Backward && tag == null) throw new ArgumentNullException(nameof(tag), "Argument 'tag' cannot be null when 'type' is 'Backward'.");
        if (!directlySet)
        {
            if (!Instance.mainFrm.Navigate(page))
            {
                s_logger.Error($"""
                                导航页面失败:
                                {type} : {page}
                                TAG: {tag ?? "Null"}
                                """);
                if (thrown)
                {
                    throw new Exception("Navigate failed");
                }
            }
        }
        else
        {
            var pageInstance = (PageBase) Activator.CreateInstance(page)!;
            Instance.mainFrm.Content = pageInstance;
        }
        ((PageBase)Instance.mainFrm.Content).ProcessParameter(param); 
        ((PageBase)Instance.mainFrm.Content).Title = I18nManager.Instance.GetString(((PageBase)Instance.mainFrm.Content).Title);
        s_isCodeChangeSelection = true;
        Instance.mnv.SelectedItem = item;
        s_isCodeChangeSelection = false;

        switch (type)
        {
            case NavigateType.Append:
            {
                Instance.BreadCrumbItemSource.Add(new BreadCrumbBarItem(
                    new PageNavigateWay(
                        page,
                        param,
                        item),
                    ((PageBase)Instance.mainFrm.Content).Title,
                    Guid.NewGuid().ToString()));
                break;
            }
            case NavigateType.New:
            {
                Instance.BreadCrumbItemSource.Clear();
                Instance.BreadCrumbItemSource.Add(new BreadCrumbBarItem(
                    new PageNavigateWay(
                        page,
                        param,
                        item),
                    ((PageBase)Instance.mainFrm.Content).Title,
                    Guid.NewGuid().ToString()));
                break;
            }
            case NavigateType.Backward:
            {
                var bcbitem = Instance.BreadCrumbItemSource.ToList().Find(bcbi => bcbi.Tag == tag);
                if(bcbitem == null) throw new InvalidOperationException("BreadCrumbItem was not found.");
                int index = Instance.BreadCrumbItemSource.IndexOf(bcbitem);
                for (int i = Instance.BreadCrumbItemSource.Count - 1; i > index; i--)
                {
                    Instance.BreadCrumbItemSource.RemoveAt(i);
                }
                break;
            }
        }
        Instance.mainBcb.ItemsSource = Instance.BreadCrumbItemSource;
    }

    private void Mnv_OnSelectionChanged(object? sender, NavigationViewSelectionChangedEventArgs e)
    {
        if (s_isCodeChangeSelection)
        {
            return;
        }
        
        if(!(mnv.SelectedItem is NavigationViewItem item)) return;
        foreach (var t in s_directNavigationPages)
        {
            if (t.IsSubclassOf(typeof(PageBase)))
            {
                var tag = ((PageBase) Activator.CreateInstance(t)).Tag.ToString();
                if (tag.Equals(item.Tag))
                {
                    NavigatePage(t, NavigateType.New, item);
                }
            }
        }
    }

    private void MainBcb_OnItemClicked(BreadcrumbBar sender, BreadcrumbBarItemClickedEventArgs args)
    {
        var item = BreadCrumbItemSource[args.Index];
        NavigatePage(item.PageNavigateWay, NavigateType.Backward, item.Tag);
    }
}

public class PageNavigateWay(Type pageType, object? param, NavigationViewItem item, bool directlySet = false)
{
    public PageNavigateWay(Type pageType, NavigationViewItem item) : this(pageType, null, item)
    {}

    public Type PageType { get; set; } = pageType;
    public object? Param { get; set; } = param;
    public NavigationViewItem Item { get; set; } = item;
    public bool DirectlySet { get; set; } = directlySet;
}

public class BreadCrumbBarItem(PageNavigateWay pageNavigateWay, string title, string tag)
{

    public PageNavigateWay PageNavigateWay { get; set; } = pageNavigateWay;
    public string Title { get; set; } = title;
    public string Tag { get; set; } = tag;
}

public enum NavigateType
{
    Append,
    New,
    Backward
}