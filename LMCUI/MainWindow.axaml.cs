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
using FluentAvalonia.UI.Windowing;
using LMC.Basic.Logging;
using LMCUI.Controls;
using LMCUI.I18n;
using LMCUI.Navigation;
using LMCUI.Pages;
using LMCUI.Pages.AccountPage;
using LMCUI.Pages.Help;
using LMCUI.Pages.LaunchPage;
using LMCUI.Pages.SettingsPage;
using LMCUI.Pages.TaskPage;
using LMCUI.Pages.VersionManagePage;

namespace LMCUI;

public partial class MainWindow : FAAppWindow
{
    private readonly static Logger s_logger = new("MainWindow");

    public static MainWindow Instance { get; private set; } = null!;

    public ObservableCollection<BreadCrumbBarItem> BreadCrumbItemSource = new();

    private readonly Dictionary<string, Type> _navItemTagToPageType = new();

    public MainWindow()
    {
        Instance = this;
        InitializeComponent();
        SplashScreen = new LineSplashScreen();
        Loaded += OnLoaded;
        InitializeNavigationMapping();
    }

    private void InitializeNavigationMapping()
    {
        _navItemTagToPageType["LaunchPage"] = typeof(LaunchPage);
        _navItemTagToPageType["VersionManagePage"] = typeof(VersionManagePage);
        _navItemTagToPageType["DownloadPage"] = typeof(LaunchPage);
        _navItemTagToPageType["AccountPage"] = typeof(AccountPage);
        _navItemTagToPageType["TaskPage"] = typeof(TaskPage);
        _navItemTagToPageType["SettingsPage"] = typeof(SettingsPage);
        _navItemTagToPageType["HelpPage"] = typeof(HelpPage);
    }

    private void OnLoaded(object? sender, RoutedEventArgs e)
    {
        mnv.SettingsItem.Tag = "SettingsPage";
        mnv.SettingsItem.Content = I18nManager.Instance.GetString("MainWindow.NavItems.SettingsPage");
        mainFrm.Navigated += OnFrameNavigated;
    }

    private void OnFrameNavigated(object? sender, NavigationEventArgs e)
    {
        e.Page.OnNavigatedTo();
    }

    public static void NavigatePage(PageNavigateWay way, NavigateType type, string? tag = null, bool thrown = false)
    {
        NavigatePage(way.PageType, type, way.Item, way.Param, tag, thrown, way.DirectlySet);
    }

    private static void NavigatePage(
        Type page,
        NavigateType type,
        FANavigationViewItem item,
        object? param = null,
        string? tag = null,
        bool thrown = false,
        bool directlySet = false)
    {
        switch (type)
        {
            case NavigateType.Backward when tag == null:
                throw new ArgumentNullException(nameof(tag), "Argument 'tag' cannot be null when 'type' is 'Backward'.");

            case NavigateType.Backward:
                NavigateBack(tag, thrown);
                return;
        }

        if (type == NavigateType.New)
        {
            item = Instance.ResolveNavigationItem(page) ?? item;
        }

        Instance.mainFrm.SlideDirection = type == NavigateType.Append
            ? SlideDirection.Right
            : SlideDirection.Bottom;

        PageBase pageInstance;

        if (directlySet)
        {
            pageInstance = (PageBase)Activator.CreateInstance(page)!;
            pageInstance.ProcessParameter(param);
            Instance.mainFrm.SetContent(pageInstance);
        }
        else
        {
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
                    导航到页面失败:
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
        UpdateNavigationSelection(item);
        HandleBreadcrumb(type, pageInstance, item, param);
    }

    private static void NavigateBack(string tag, bool thrown)
    {
        var bcbItem = Instance.BreadCrumbItemSource.ToList().Find(bcbi => bcbi.Tag == tag);
        if (bcbItem == null)
        {
            s_logger.Error($"BreadCrumbItem未找到: {tag}");
            if (thrown)
            {
                throw new InvalidOperationException($"BreadCrumbItem not found: {tag}");
            }

            return;
        }

        var way = bcbItem.PageNavigateWay;
        UpdateNavigationSelection(way.Item);
        Instance.mainFrm.SlideDirection = SlideDirection.Left;

        var registration = PageRegistry.Instance.GetByType(way.PageType);
        if (registration != null)
        {
            Instance.mainFrm.NavigateWithoutHistory(registration, way.Param);
        }

        var index = Instance.BreadCrumbItemSource.IndexOf(bcbItem);
        for (int i = Instance.BreadCrumbItemSource.Count - 1; i > index; i--)
        {
            Instance.BreadCrumbItemSource.RemoveAt(i);
        }
    }

    private static void UpdateNavigationSelection(FANavigationViewItem item)
    {
        Instance.mnv.SelectedItem = item;
    }

    private FANavigationViewItem? ResolveNavigationItem(Type pageType)
    {
        var matchedTag = _navItemTagToPageType
            .FirstOrDefault(pair => pair.Value == pageType)
            .Key;

        if (string.IsNullOrEmpty(matchedTag))
        {
            return null;
        }

        return FindNavigationItemByTag(matchedTag);
    }

    private FANavigationViewItem? FindNavigationItemByTag(string tag)
    {
        if (mnv.SettingsItem?.Tag?.ToString() == tag)
        {
            return mnv.SettingsItem;
        }

        return FindNavigationItemByTag(mnv.MenuItems, tag)
               ?? FindNavigationItemByTag(mnv.FooterMenuItems, tag);
    }

    private static FANavigationViewItem? FindNavigationItemByTag(IEnumerable<object>? items, string tag)
    {
        if (items == null)
        {
            return null;
        }

        foreach (var menuItem in items.OfType<FANavigationViewItem>())
        {
            if (menuItem.Tag?.ToString() == tag)
            {
                return menuItem;
            }

            var nestedItem = FindNavigationItemByTag(menuItem.MenuItems, tag);
            if (nestedItem != null)
            {
                return nestedItem;
            }
        }

        return null;
    }

    private static void HandleBreadcrumb(NavigateType type, PageBase page, FANavigationViewItem item, object? param)
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

    private void Mnv_OnSelectionChanged(object? sender, FANavigationViewSelectionChangedEventArgs e)
    {
        if (e.SelectedItem is not FANavigationViewItem item)
        {
            return;
        }

        var tag = item.Tag?.ToString();
        if (tag == null)
        {
            return;
        }

        if (_navItemTagToPageType.TryGetValue(tag, out var pageType))
        {
            NavigatePage(pageType, NavigateType.New, item);
            return;
        }

        var registration = PageRegistry.Instance.MatchByTag(tag);
        if (registration != null)
        {
            NavigatePage(registration.PageType, NavigateType.New, item);
        }
    }

    private void MainBcb_OnItemClicked(FABreadcrumbBar sender, FABreadcrumbBarItemClickedEventArgs args)
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
