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
using System.Collections.ObjectModel;
using System.Linq;
using Avalonia.Interactivity;
using FluentAvalonia.UI.Controls;
using LMCUI.Navigation;
using FluentAvalonia.UI.Windowing;
using LMC.Basic.Logging;
using LMCUI.Controls;
using LMCUI.I18n;
using LMCUI.Pages;
using LMCUI.Pages.AccountPage;
using LMCUI.Pages.Help;
using LMCUI.Pages.LaunchPage;
using LMCUI.Pages.SettingsPage;
using LMCUI.Pages.TaskPage;
using LMCUI.Pages.VersionManagePage;

namespace LMCUI;

public partial class MainWindow : AppWindow
{
    private readonly static Logger s_logger = new Logger("MainWindow");
    
    public static MainWindow Instance { get; private set; } = null!;
    public ObservableCollection<BreadCrumbBarItem> BreadCrumbItemSource = new ObservableCollection<BreadCrumbBarItem>();

    /// <summary>
    /// 导航视图项与页面类型的映射
    /// </summary>
    private readonly Dictionary<string, Type> _navItemTagToPageType = new();

    public MainWindow()
    {
        Instance = this;
        InitializeComponent();
        SplashScreen = new LineSplashScreen();
        Loaded += OnLoaded;
        InitializeNavigationMapping();
    }

    /// <summary>
    /// 初始化导航映射
    /// </summary>
    private void InitializeNavigationMapping()
    {
        // 主导航项
        _navItemTagToPageType["LaunchPage"] = typeof(LaunchPage);
        _navItemTagToPageType["VersionManagePage"] = typeof(VersionManagePage);
        _navItemTagToPageType["DownloadPage"] = typeof(LaunchPage); // TODO: 未来实现下载页面
        _navItemTagToPageType["AccountPage"] = typeof(AccountPage);
        _navItemTagToPageType["TaskPage"] = typeof(TaskPage);
        _navItemTagToPageType["SettingsPage"] = typeof(SettingsPage);
        _navItemTagToPageType["HelpPage"] = typeof(HelpPage);
    }

    private void OnLoaded(object? sender, RoutedEventArgs e)
    {
        mnv.SettingsItem.Tag = "SettingsPage";
        mnv.SettingsItem.Content = I18nManager.Instance.GetString("MainWindow.NavItems.SettingsPage");

        // 初始化默认页面
        mainFrm.Navigated += OnFrameNavigated;
    }

    private void OnFrameNavigated(object? sender, NavigationEventArgs e)
    {
        // 通知页面
        e.Page.OnNavigatedTo();
    }

    /// <summary>
    /// 导航到页面
    /// </summary>
    public static void NavigatePage(PageNavigateWay way, NavigateType type, string? tag = null, bool thrown = false)
    {
        NavigatePage(way.PageType, type, way.Item, way.Param, tag, thrown, way.DirectlySet);
    }

    /// <summary>
    /// 导航到页面
    /// </summary>
    private static void NavigatePage(Type page, NavigateType type, NavigationViewItem item, object? param = null, string? tag = null, bool thrown = false, bool directlySet = false)
    {
        switch (type)
        {
            case NavigateType.Backward when tag == null:
                throw new ArgumentNullException(nameof(tag), "Argument 'tag' cannot be null when 'type' is 'Backward'.");

            case NavigateType.Backward:
                NavigateBack(tag, thrown);
                return;
        }

        // 根据导航类型设置滑动方向
        // Append: 从右侧滑入，New: 从下方滑入
        Instance.mainFrm.SlideDirection = type == NavigateType.Append
            ? SlideDirection.Right
            : SlideDirection.Bottom;

        PageBase pageInstance;
        
        if (directlySet)
        {
            // 直接创建实例（绕过 Frame 的缓存逻辑）
            pageInstance = (PageBase)Activator.CreateInstance(page)!;
            pageInstance.ProcessParameter(param);
            Instance.mainFrm.SetContent(pageInstance);
        }
        else
        {
            // 使用 Frame 导航
            var registration = PageRegistry.Instance.GetByType(page);
            if (registration == null)
            {
                s_logger.Error($"""
                    页面未注册: {page}
                    """);
                if (thrown)
                {
                    throw new Exception($"Page not registered: {page}");
                }
                return;
            }

            if (!Instance.mainFrm.Navigate(registration, param))
            {
                s_logger.Error($"""
                    导航页面失败:
                    {type} : {page}
                    """);
                if (thrown)
                {
                    throw new Exception("Navigate failed");
                }
                return;
            }
            pageInstance = Instance.mainFrm.CurrentPage!;
        }

        pageInstance.Title = I18nManager.Instance.GetString(pageInstance.Title);
        

        // 更新导航选中项
        UpdateNavigationSelection(item);

        // 处理面包屑
        HandleBreadcrumb(type, pageInstance, item, param);
    }

    /// <summary>
    /// 导航返回（面包屑点击）- 从左侧滑入
    /// </summary>
    private static void NavigateBack(string tag, bool thrown)
    {
        // 查找面包屑项
        var bcbItem = Instance.BreadCrumbItemSource.ToList().Find(bcbi => bcbi.Tag == tag);
        if (bcbItem == null)
        {
            s_logger.Error($"未找到面包屑项: {tag}");
            if (thrown)
            {
                throw new InvalidOperationException($"BreadCrumbItem not found: {tag}");
            }
            return;
        }

        // 更新选中项
        var way = bcbItem.PageNavigateWay;
        UpdateNavigationSelection(way.Item);

        // 返回时使用从左侧滑入的方向
        Instance.mainFrm.SlideDirection = SlideDirection.Left;

        // 导航到目标页面（不记录历史，因为这是返回操作）
        var registration = PageRegistry.Instance.GetByType(way.PageType);
        if (registration != null)
        {
            Instance.mainFrm.NavigateWithoutHistory(registration, way.Param);
        }

        // 移除后续的面包屑项（不包括当前点击的项）
        var index = Instance.BreadCrumbItemSource.IndexOf(bcbItem);
        for (int i = Instance.BreadCrumbItemSource.Count - 1; i > index; i--)
        {
            Instance.BreadCrumbItemSource.RemoveAt(i);
        }
    }

    /// <summary>
    /// 更新导航选中项
    /// </summary>
    private static void UpdateNavigationSelection(NavigationViewItem item)
    {
        Instance.mnv.SelectedItem = item;
    }

    /// <summary>
    /// 处理面包屑导航
    /// </summary>
    private static void HandleBreadcrumb(NavigateType type, PageBase page, NavigationViewItem item, object? param)
    {
        switch (type)
        {
            case NavigateType.Append:
                Instance.BreadCrumbItemSource.Add(new BreadCrumbBarItem(
                    new PageNavigateWay(page.GetType(), param, item),
                    page.Title,
                    Guid.NewGuid().ToString()));
                break;

            case NavigateType.New:
                Instance.BreadCrumbItemSource.Clear();
                Instance.BreadCrumbItemSource.Add(new BreadCrumbBarItem(
                    new PageNavigateWay(page.GetType(), param, item),
                    page.Title,
                    Guid.NewGuid().ToString()));
                break;
        }
        Instance.mainBcb.ItemsSource = Instance.BreadCrumbItemSource;
    }

    /// <summary>
    /// 导航视图选择改变事件
    /// </summary>
    private void Mnv_OnSelectionChanged(object? sender, NavigationViewSelectionChangedEventArgs e)
    {
        if (e.SelectedItem is not NavigationViewItem item)
        {
            return;
        }

        var tag = item.Tag?.ToString();
        if (tag == null)
        {
            return;
        }

        // 尝试从映射中获取页面类型
        if (_navItemTagToPageType.TryGetValue(tag, out var pageType))
        {
            NavigatePage(pageType, NavigateType.New, item);
            return;
        }

        // 尝试从注册表匹配
        var registration = PageRegistry.Instance.MatchByTag(tag);
        if (registration != null)
        {
            NavigatePage(registration.PageType, NavigateType.New, item);
        }
    }

    private void MainBcb_OnItemClicked(BreadcrumbBar sender, BreadcrumbBarItemClickedEventArgs args)
    {
        var item = BreadCrumbItemSource[args.Index];
        NavigatePage(item.PageNavigateWay, NavigateType.Backward, item.Tag);
    }

    public bool CanGoBack => mainFrm.CanGoBack;

    public void GoBack()
    {
        if (mainFrm.CanGoBack)
        {
            mainFrm.GoBack();
            if (BreadCrumbItemSource.Count > 0)
            {
                BreadCrumbItemSource.RemoveAt(BreadCrumbItemSource.Count - 1);
            }
        }
    }
}
