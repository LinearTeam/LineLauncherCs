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
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using FluentAvalonia.UI.Controls;
using LMC.Basic.Logging;
using LMCCore.Game.Model;
using LMCCore.Game.Versioning;
using LMCUI.I18n;
using LMCUI.Navigation;
using LMCUI.Navigation.Model;
using LMCUI.Utils;

namespace LMCUI.Pages.VersionManagePage;

public partial class VersionManagePage : PageBase
{
    private readonly Logger _logger = new("VersionManagePage");
    private readonly VersionManager _versionManager = new();
    private readonly VersionConfigManager _versionConfigManager = new();
    private readonly ObservableCollection<VersionRenderData> _visibleVersions = [];
    private readonly Dictionary<string, LocalGameVersionEntry> _versionNameMap = [];
    private readonly FuncDataTemplate<VersionRenderData> _versionItemTemplate;
    private readonly VersionManagePageRefreshState _refreshState = new();

    private CancellationTokenSource? _refreshCts;
    private FileSystemWatcher? _versionsWatcher;
    private DispatcherTimer? _refreshDebounceTimer;
    private bool _isPageLoaded;
    private string? _watchedRootPath;
    private string _lastInvalidVersionSignature = string.Empty;
    private int _pendingRefreshToken;
    private readonly static TimeSpan MinimumLoadingDuration = TimeSpan.FromMilliseconds(500);

    public VersionManagePage() : base("Pages.VersionManagePage.Title", "VersionManagePage")
    {
        _versionItemTemplate = new FuncDataTemplate<VersionRenderData>((renderData, _) => CreateVersionExpander(renderData), true);
        InitializeComponent();
        VersionListBox.ItemTemplate = _versionItemTemplate;
        VersionListBox.ItemsSource = _visibleVersions;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    public void RefreshPage()
    {
        _ = RefreshPageAsync();
    }

    private FASettingsExpander CreateVersionExpander(VersionRenderData renderData)
    {
        var expander = new FASettingsExpander
        {
            IsClickEnabled = true,
            Header = renderData.Version.VersionName,
            Description = renderData.Description,
            ActionIconSource = new FASymbolIconSource
            {
                Symbol = FASymbol.ChevronRight
            },
            IconSource = CreateVersionIconSource(renderData),
            Tag = renderData.Version.VersionName,
            HorizontalAlignment = HorizontalAlignment.Stretch
        };

        expander.Click += VersionExpander_OnClick;
        return expander;
    }

    public void NavigateToVersionDetail(LocalGameVersionEntry version)
    {
        if (MainWindow.Instance.mnv.SelectedItem is not FANavigationViewItem selectedItem)
        {
            return;
        }

        MainWindow.NavigatePage(
            new PageNavigateWay(typeof(VersionDetailPage), version, selectedItem),
            NavigateType.Append);
    }

    public async void ShowRootManagerDialog()
    {
        var closeButtonText = I18nManager.Instance.GetString("Pages.VersionManagePage.RootDialog.Cancel");
        var isDialogBusy = false;

        var dialog = new FAContentDialog
        {
            Title = new TextBlock
            {
                Text = I18nManager.Instance.GetString("Pages.VersionManagePage.RootDialog.Title"),
                FontSize = 15,
                FontWeight = FontWeight.Light
            },
            PrimaryButtonText = I18nManager.Instance.GetString("Pages.VersionManagePage.RootDialog.Confirm"),
            CloseButtonText = closeButtonText,
            DefaultButton = FAContentDialogButton.Primary
        };

        var rootList = new ListBox
        {
            SelectionMode = SelectionMode.Single,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            MinWidth = 420
        };

        RefreshRootList(rootList);

        var addButton = new Button
        {
            Content = I18nManager.Instance.GetString("Pages.VersionManagePage.RootDialog.Add")
        };
        addButton.Click += async (_, _) => { await AddRootAsync(rootList); };

        var deleteButton = new Button
        {
            Content = I18nManager.Instance.GetString("Pages.VersionManagePage.RootDialog.Delete"),
            Foreground = Brushes.Red
        };
        deleteButton.Click += (_, _) => { DeleteSelectedRoot(rootList); };

        void SetDialogBusyState(bool isBusy)
        {
            isDialogBusy = isBusy;
            dialog.IsPrimaryButtonEnabled = !isBusy;
            dialog.IsSecondaryButtonEnabled = !isBusy;
            dialog.CloseButtonText = isBusy ? string.Empty : closeButtonText;
        }

        var rootDialogContent = CreateRootDialogContent(rootList, addButton, deleteButton);
        dialog.Content = rootDialogContent;
        dialog.PrimaryButtonClick += async (_, args) =>
        {
            if (rootList.SelectedItem is not ListBoxItem { Tag: ManagedGameRoot selectedRoot })
            {
                args.Cancel = true;
                return;
            }

            args.Cancel = true;
            SetDialogBusyState(true);
            dialog.Content = CreateCenteredProgressContainer(
                I18nManager.Instance.GetString("Pages.VersionManagePage.EmptyState.Loading"));

            try
            {
                await Task.Run(() => _versionManager.SetSelectedRoot(selectedRoot.RootPath));
                await RefreshPageAsync(forceInvalidNotification: true);
                SetDialogBusyState(false);
                dialog.Hide();
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Switching game root");
                await MessageQueueHelper.ShowError(
                    I18nManager.Instance.GetString("Pages.VersionManagePage.Errors.SwitchRootTitle"),
                    I18nManager.Instance.GetString("Pages.VersionManagePage.Errors.SwitchRootContent", ex.Message));
                SetDialogBusyState(false);
                dialog.Content = rootDialogContent;
            }
        };
        dialog.CloseButtonClick += (_, args) =>
        {
            if (isDialogBusy)
            {
                args.Cancel = true;
            }
        };
        dialog.Closing += (_, args) =>
        {
            if (isDialogBusy)
            {
                args.Cancel = true;
            }
        };

        await dialog.ShowAsync();
    }

    private void OnLoaded(object? sender, RoutedEventArgs e)
    {
        _isPageLoaded = true;
        EnsureRefreshDebounceTimer();
        MainWindow.Instance.Activated += MainWindow_OnActivated;
        RefreshPage();
    }

    private void OnUnloaded(object? sender, RoutedEventArgs e)
    {
        _isPageLoaded = false;
        MainWindow.Instance.Activated -= MainWindow_OnActivated;
        DisposeWatcher();
        _refreshDebounceTimer?.Stop();
        _refreshCts?.Cancel();
    }

    async private Task RefreshPageAsync(bool forceInvalidNotification = false)
    {
        _refreshCts?.Cancel();
        _refreshCts = new CancellationTokenSource();
        var cancellationToken = _refreshCts.Token;
        var loadingStartedUtc = DateTime.UtcNow;
        var selectedRoot = _versionManager.GetSelectedRoot();
        UpdateCurrentRootDisplay(selectedRoot, null);

        if (selectedRoot == null)
        {
            DisposeWatcher();
            _lastInvalidVersionSignature = string.Empty;
            _refreshState.ClearPendingExternalRefresh();
            RenderEmptyState(I18nManager.Instance.GetString("Pages.VersionManagePage.EmptyState.NoRootVersions"));
            return;
        }

        ShowVersionLoadingState();
        await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Render);

        List<LocalGameVersionEntry> versions;
        try
        {
            versions = (await _versionManager.ScanVersionsAsync(selectedRoot.RootPath, cancellationToken)).ToList();
        }
        catch (OperationCanceledException)
        {
            return;
        }
        catch (Exception ex)
        {
            await EnsureMinimumLoadingDurationAsync(loadingStartedUtc, cancellationToken);
            if (cancellationToken.IsCancellationRequested)
            {
                return;
            }

            _logger.Error(ex, "Scanning versions");
            RenderEmptyState(I18nManager.Instance.GetString("Pages.VersionManagePage.Errors.ScanFailedContent", ex.Message));
            await MessageQueueHelper.ShowError(
                I18nManager.Instance.GetString("Pages.VersionManagePage.Errors.ScanFailedTitle"),
                I18nManager.Instance.GetString("Pages.VersionManagePage.Errors.ScanFailedContent", ex.Message));
            return;
        }

        if (cancellationToken.IsCancellationRequested)
        {
            return;
        }

        IReadOnlyList<VersionRenderData> renderData;
        try
        {
            renderData = await BuildVersionRenderDataAsync(versions, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        await EnsureMinimumLoadingDurationAsync(loadingStartedUtc, cancellationToken);

        if (cancellationToken.IsCancellationRequested)
        {
            return;
        }

        _refreshState.MarkRefreshed(DateTime.UtcNow);
        UpdateCurrentRootDisplay(selectedRoot, versions.Count);
        RenderVersions(renderData);
        NotifyInvalidVersions(versions, forceInvalidNotification);
        ConfigureWatcher(selectedRoot.RootPath);
    }

    private void UpdateCurrentRootDisplay(ManagedGameRoot? selectedRoot, int? versionCount)
    {
        if (selectedRoot == null)
        {
            CurrentRootExpander.Header = I18nManager.Instance.GetString("Pages.VersionManagePage.EmptyState.NoRootHeader");
            CurrentRootExpander.Description = I18nManager.Instance.GetString("Pages.VersionManagePage.EmptyState.NoRootDescription");
            return;
        }

        CurrentRootExpander.Header = I18nManager.Instance.GetString("Pages.VersionManagePage.CurrentRoot.Header");
        CurrentRootExpander.Description = versionCount == null
            ? selectedRoot.RootPath
            : VersionManagePagePresentation.BuildCurrentRootDescription(
                selectedRoot,
                I18nManager.Instance.GetString("Pages.VersionManagePage.CurrentRoot.VersionCount", versionCount.Value));
    }

    private void ShowVersionLoadingState()
    {
        ShowStateContent(CreateCenteredProgressContainer(
            I18nManager.Instance.GetString("Pages.VersionManagePage.EmptyState.Loading")));
    }

    private void RenderVersions(IReadOnlyList<VersionRenderData> renderData)
    {
        _visibleVersions.Clear();
        _versionNameMap.Clear();
        VersionListBox.ItemTemplate = _versionItemTemplate;
        VersionListBox.ItemsSource = _visibleVersions;

        if (renderData.Count == 0)
        {
            RenderEmptyState(I18nManager.Instance.GetString("Pages.VersionManagePage.EmptyState.NoVersions"));
            return;
        }

        ShowRepeater();

        foreach (var item in renderData)
        {
            _visibleVersions.Add(item);
            _versionNameMap[item.Version.VersionName] = item.Version;
        }
    }

    private void RenderEmptyState(string text)
    {
        _visibleVersions.Clear();
        _versionNameMap.Clear();
        ShowStateContent(new TextBlock
        {
            Text = text,
            TextWrapping = TextWrapping.Wrap,
            Opacity = 0.75,
            Margin = new Thickness(0, 8, 0, 0)
        });
    }

    private Control CreateCenteredProgressContainer(string text)
    {
        return new Grid
        {
            Height = 220,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Center,
            Children =
            {
                new StackPanel
                {
                    Orientation = Orientation.Vertical,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    Spacing = 10,
                    Children =
                    {
                        new FAProgressRing
                        {
                            IsActive = true,
                            IsIndeterminate = true,
                            Width = 56,
                            Height = 56
                        },
                        new TextBlock
                        {
                            Text = text,
                            HorizontalAlignment = HorizontalAlignment.Center,
                            Opacity = 0.8
                        }
                    }
                }
            }
        };
    }

    private void ShowRepeater()
    {
        VersionListStateHost.Content = null;
        VersionListStateHost.IsVisible = false;
        VersionListBox.IsVisible = true;
    }

    private void ShowStateContent(Control content)
    {
        VersionListBox.IsVisible = false;
        VersionListStateHost.Content = content;
        VersionListStateHost.IsVisible = true;
    }

    private Control CreateRootDialogContent(ListBox rootList, Button addButton, Button deleteButton)
    {
        return new StackPanel
        {
            Orientation = Orientation.Vertical,
            Spacing = 10,
            Children =
            {
                rootList,
                new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Spacing = 6,
                    HorizontalAlignment = HorizontalAlignment.Right,
                    Children =
                    {
                        addButton,
                        deleteButton
                    }
                }
            }
        };
    }

    private void RefreshRootList(ListBox rootList)
    {
        var rootItems = VersionManagePagePresentation.BuildRootListItems(_versionManager.GetManagedRoots());
        var items = rootItems
            .Select(item => new ListBoxItem
        {
            Tag = item.Root,
            Content = new StackPanel
            {
                Orientation = Orientation.Vertical,
                Children =
                {
                    new TextBlock
                    {
                        Text = item.Header,
                        FontWeight = FontWeight.SemiBold
                    },
                    new TextBlock
                    {
                        Text = item.Description,
                        TextWrapping = TextWrapping.Wrap,
                        Opacity = 0.8
                    }
                }
            }
        }).ToList();

        rootList.ItemsSource = items;
        rootList.SelectedIndex = VersionManagePagePresentation.GetSelectedRootIndex(rootItems, _versionManager.GetSelectedRoot());
    }

    async private Task AddRootAsync(ListBox rootList)
    {
        try
        {
            var folders = await CrossPlatformUtils.OpenFolderPickerAsync(new FolderPickerOpenOptions
            {
                AllowMultiple = false,
                Title = I18nManager.Instance.GetString("Pages.VersionManagePage.RootDialog.PickFolderTitle")
            });
            var folder = folders.FirstOrDefault();
            if (folder == null)
            {
                return;
            }

            _versionManager.AddManagedRoot(folder.Path.LocalPath);
            RefreshRootList(rootList);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Adding game root");
            await MessageQueueHelper.ShowError(
                I18nManager.Instance.GetString("Pages.VersionManagePage.Errors.AddRootTitle"),
                I18nManager.Instance.GetString("Pages.VersionManagePage.Errors.AddRootContent", ex.Message));
        }
    }

    private void DeleteSelectedRoot(ListBox rootList)
    {
        if (rootList.SelectedItem is not ListBoxItem { Tag: ManagedGameRoot selectedRoot })
        {
            return;
        }

        _versionManager.RemoveManagedRoot(selectedRoot.RootPath);
        RefreshRootList(rootList);
    }

    private void VersionExpander_OnClick(object? sender, RoutedEventArgs e)
    {
        if (sender is FASettingsExpander { Tag: string versionName } && _versionNameMap.TryGetValue(versionName, out var version))
        {
            NavigateToVersionDetail(version);
        }
    }

    private void InstallVersionButton_OnClick(object? sender, RoutedEventArgs e)
    {
        MainWindow.NavigatePage(new PageNavigateWay(
            typeof(LaunchPage.LaunchPage),
            (FANavigationViewItem)MainWindow.Instance.mnv.SelectedItem), NavigateType.Append);
    }

    private void SwitchGameRootButton_OnClick(object? sender, RoutedEventArgs e)
    {
        ShowRootManagerDialog();
    }

    async private void CurrentRootExpander_OnClick(object? sender, RoutedEventArgs e)
    {
        var selectedRoot = _versionManager.GetSelectedRoot();
        if (selectedRoot == null || !Directory.Exists(selectedRoot.RootPath))
        {
            return;
        }

        try
        {
            CrossPlatformUtils.OpenFolderInExplorer(Path.GetFullPath(selectedRoot.RootPath));
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Opening selected game root");
            await MessageQueueHelper.ShowError(
                I18nManager.Instance.GetString("Pages.VersionManagePage.Errors.SwitchRootTitle"),
                I18nManager.Instance.GetString("Pages.VersionManagePage.Errors.SwitchRootContent", ex.Message));
        }
    }

    private string GetDisplayTypeText(VersionDisplayType displayType)
    {
        return displayType switch
        {
            VersionDisplayType.Snapshot => I18nManager.Instance.GetString("Pages.VersionManagePage.VersionType.Snapshot"),
            VersionDisplayType.AprilFools => I18nManager.Instance.GetString("Pages.VersionManagePage.VersionType.AprilFools"),
            VersionDisplayType.Old => I18nManager.Instance.GetString("Pages.VersionManagePage.VersionType.Old"),
            VersionDisplayType.Error => I18nManager.Instance.GetString("Pages.VersionManagePage.VersionType.Error"),
            _ => I18nManager.Instance.GetString("Pages.VersionManagePage.VersionType.Release")
        };
    }

    private string GetStatusText(VersionStatus status)
    {
        return status switch
        {
            VersionStatus.Valid => I18nManager.Instance.GetString("Pages.VersionManagePage.VersionStatus.Valid"),
            VersionStatus.MissingJar => I18nManager.Instance.GetString("Pages.VersionManagePage.VersionStatus.MissingJar"),
            VersionStatus.MissingJson => I18nManager.Instance.GetString("Pages.VersionManagePage.VersionStatus.MissingJson"),
            VersionStatus.InvalidJson => I18nManager.Instance.GetString("Pages.VersionManagePage.VersionStatus.InvalidJson"),
            _ => status.ToString()
        };
    }

    async private Task<IReadOnlyList<VersionRenderData>> BuildVersionRenderDataAsync(
        IReadOnlyList<LocalGameVersionEntry> versions,
        CancellationToken cancellationToken)
    {
        return await Task.Run(() =>
        {
            var results = new List<VersionRenderData>(versions.Count);
            foreach (var version in versions)
            {
                cancellationToken.ThrowIfCancellationRequested();
                results.Add(BuildVersionRenderData(version));
            }

            return (IReadOnlyList<VersionRenderData>)results;
        }, cancellationToken);
    }

    private VersionRenderData BuildVersionRenderData(LocalGameVersionEntry version)
    {
        return VersionManagePagePresentation.BuildVersionRenderData(
            version,
            GetDisplayTypeText,
            I18nManager.Instance.GetString("Pages.VersionManagePage.VersionType.UnknownClientVersion"),
            ResolveVersionIcon);
    }

    private FAIconSource CreateVersionIconSource(VersionRenderData renderData)
    {
        try
        {
            return renderData.IconKind switch
            {
                VersionIconKind.Asset when renderData.IconPath != null => new FABitmapIconSource
                {
                    UriSource = new Uri($"avares://LMCUI{renderData.IconPath}")
                },
                VersionIconKind.File when renderData.IconPath != null => new FABitmapIconSource
                {
                    UriSource = new Uri(renderData.IconPath)
                },
                _ => CreateFallbackIconSource(renderData.DisplayType)
            };
        }
        catch (FileNotFoundException ex) when (IsMissingBuiltInVersionIcon(renderData, ex))
        {
            _logger.Debug($"忽略缺失的内置版本图标资源: {renderData.IconPath}");
            return CreateFallbackIconSource(renderData.DisplayType);
        }
    }

    private static FAIconSource CreateFallbackIconSource(VersionDisplayType displayType)
    {
        return new FASymbolIconSource
        {
            Symbol = displayType == VersionDisplayType.Error ? FASymbol.Important : FASymbol.Games
        };
    }

    private string GetBuiltInIconResourcePath(VersionDisplayType displayType)
    {
        return VersionManagePagePresentation.GetBuiltInIconResourcePath(displayType);
    }

    private (VersionIconKind IconKind, string? IconPath) ResolveVersionIcon(
        VersionDisplayType displayType,
        LocalGameVersionEntry version)
    {
        var customIconPath = displayType == VersionDisplayType.Error
            ? null
            : _versionConfigManager.GetValue<string>(version, "iconPath");
        return VersionManagePagePresentation.ResolveVersionIcon(displayType, customIconPath);
    }

    private static bool IsMissingBuiltInVersionIcon(VersionRenderData renderData, FileNotFoundException exception)
    {
        return renderData.IconKind == VersionIconKind.Asset &&
               renderData.IconPath?.StartsWith("/Assets/VersionIcons/", StringComparison.OrdinalIgnoreCase) == true &&
               exception.Message.Contains("avares://LMCUI/Assets/VersionIcons/", StringComparison.OrdinalIgnoreCase);
    }

    private void NotifyInvalidVersions(IReadOnlyList<LocalGameVersionEntry> versions, bool forceInvalidNotification)
    {
        var notification = VersionManagePagePresentation.BuildInvalidVersionNotification(versions, GetStatusText);
        if (notification == null)
        {
            _lastInvalidVersionSignature = string.Empty;
            return;
        }

        if (!forceInvalidNotification && string.Equals(notification.Signature, _lastInvalidVersionSignature, StringComparison.Ordinal))
        {
            return;
        }

        _lastInvalidVersionSignature = notification.Signature;
        _ = MessageQueueHelper.ShowWarning(
            I18nManager.Instance.GetString("Pages.VersionManagePage.InvalidVersions.Title"),
            I18nManager.Instance.GetString("Pages.VersionManagePage.InvalidVersions.Content", string.Join("，", notification.Items)));
    }

    private void EnsureRefreshDebounceTimer()
    {
        _refreshDebounceTimer ??= new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(700)
        };
        _refreshDebounceTimer.Tick -= RefreshDebounceTimer_OnTick;
        _refreshDebounceTimer.Tick += RefreshDebounceTimer_OnTick;
    }

    private void RefreshDebounceTimer_OnTick(object? sender, EventArgs e)
    {
        _refreshDebounceTimer?.Stop();
        if (_refreshState.TryConsumeDebouncedRefresh(_pendingRefreshToken, _isPageLoaded))
        {
            RefreshPage();
        }
    }

    private void MainWindow_OnActivated(object? sender, EventArgs e)
    {
        if (_refreshState.ShouldRefreshOnActivation(_isPageLoaded, DateTime.UtcNow))
        {
            RefreshPage();
        }
    }

    private void ConfigureWatcher(string rootPath)
    {
        var normalizedRootPath = VersionManagePagePresentation.NormalizeRootPath(rootPath);

        if (string.Equals(_watchedRootPath, normalizedRootPath, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        DisposeWatcher();

        var versionsPath = Path.Combine(normalizedRootPath, "versions");
        if (!Directory.Exists(versionsPath))
        {
            _watchedRootPath = normalizedRootPath;
            return;
        }

        _versionsWatcher = new FileSystemWatcher(versionsPath)
        {
            IncludeSubdirectories = true,
            NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName | NotifyFilters.LastWrite | NotifyFilters.CreationTime,
            EnableRaisingEvents = true
        };

        _versionsWatcher.Changed += VersionsWatcher_OnChanged;
        _versionsWatcher.Created += VersionsWatcher_OnChanged;
        _versionsWatcher.Deleted += VersionsWatcher_OnChanged;
        _versionsWatcher.Renamed += VersionsWatcher_OnChanged;
        _watchedRootPath = normalizedRootPath;
    }

    private void VersionsWatcher_OnChanged(object sender, FileSystemEventArgs e)
    {
        _pendingRefreshToken = _refreshState.QueueExternalRefresh();
        Dispatcher.UIThread.Post(() =>
        {
            EnsureRefreshDebounceTimer();
            _refreshDebounceTimer?.Stop();
            _refreshDebounceTimer?.Start();
        });
    }

    private void DisposeWatcher()
    {
        if (_versionsWatcher != null)
        {
            _versionsWatcher.EnableRaisingEvents = false;
            _versionsWatcher.Changed -= VersionsWatcher_OnChanged;
            _versionsWatcher.Created -= VersionsWatcher_OnChanged;
            _versionsWatcher.Deleted -= VersionsWatcher_OnChanged;
            _versionsWatcher.Renamed -= VersionsWatcher_OnChanged;
            _versionsWatcher.Dispose();
            _versionsWatcher = null;
        }

        _watchedRootPath = null;
    }

    async private static Task EnsureMinimumLoadingDurationAsync(DateTime loadingStartedUtc, CancellationToken cancellationToken)
    {
        var elapsed = DateTime.UtcNow - loadingStartedUtc;
        var remaining = MinimumLoadingDuration - elapsed;
        if (remaining > TimeSpan.Zero)
        {
            await Task.Delay(remaining, cancellationToken);
        }
    }
}
