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

    private CancellationTokenSource? _refreshCts;
    private FileSystemWatcher? _versionsWatcher;
    private DispatcherTimer? _refreshDebounceTimer;
    private bool _pendingExternalRefresh;
    private bool _isPageLoaded;
    private string? _watchedRootPath;
    private string _lastInvalidVersionSignature = string.Empty;
    private DateTime _lastRefreshUtc = DateTime.MinValue;
    private readonly static TimeSpan MinimumLoadingDuration = TimeSpan.FromMilliseconds(500);

    private enum VersionDisplayType
    {
        Release,
        Snapshot,
        AprilFools,
        Old,
        Error
    }

    private enum VersionIconKind
    {
        Asset,
        File,
        Symbol
    }

    private sealed class VersionRenderData
    {
        public required LocalGameVersionEntry Version { get; init; }
        public required VersionDisplayType DisplayType { get; init; }
        public required string Description { get; init; }
        public required VersionIconKind IconKind { get; init; }
        public string? IconPath { get; init; }
    }

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

        _lastRefreshUtc = DateTime.UtcNow;
        _pendingExternalRefresh = false;
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
            : $"{selectedRoot.RootPath} | {I18nManager.Instance.GetString("Pages.VersionManagePage.CurrentRoot.VersionCount", versionCount.Value)}";
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
        var roots = _versionManager.GetManagedRoots().ToList();
        var items = roots.Select(root => new ListBoxItem
        {
            Tag = root,
            Content = new StackPanel
            {
                Orientation = Orientation.Vertical,
                Children =
                {
                    new TextBlock
                    {
                        Text = Path.GetFileName(root.RootPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)),
                        FontWeight = FontWeight.SemiBold
                    },
                    new TextBlock
                    {
                        Text = root.RootPath,
                        TextWrapping = TextWrapping.Wrap,
                        Opacity = 0.8
                    }
                }
            }
        }).ToList();

        rootList.ItemsSource = items;

        var selectedRoot = _versionManager.GetSelectedRoot();
        if (selectedRoot == null)
        {
            rootList.SelectedIndex = items.Count > 0 ? 0 : -1;
            return;
        }

        rootList.SelectedItem = items.FirstOrDefault(item =>
            item.Tag is ManagedGameRoot root &&
            string.Equals(root.RootPath, selectedRoot.RootPath, StringComparison.OrdinalIgnoreCase));
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

    private VersionDisplayType GetDisplayType(LocalGameVersionEntry version)
    {
        if (version.Status != VersionStatus.Valid)
        {
            return VersionDisplayType.Error;
        }

        var displayType = version.VersionInfo == null
            ? GameVersionDisplayType.Release
            : GameVersionTypeClassifier.ClassifyManifestVersion(
                version.VersionInfo.Id,
                version.VersionInfo.Type,
                version.VersionInfo.ReleaseTime,
                version.VersionName,
                version.ClientVersionId);

        return displayType switch
        {
            GameVersionDisplayType.Snapshot => VersionDisplayType.Snapshot,
            GameVersionDisplayType.AprilFools => VersionDisplayType.AprilFools,
            GameVersionDisplayType.Old => VersionDisplayType.Old,
            _ => VersionDisplayType.Release
        };
    }

    private string GetVersionDescription(LocalGameVersionEntry version, VersionDisplayType displayType)
    {
        return $"{GetDisplayTypeText(displayType)} - {GetClientVersionIdText(version.ClientVersionId)}";
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

    private string GetClientVersionIdText(string clientVersionId)
    {
        return string.Equals(clientVersionId, "未知版本", StringComparison.Ordinal)
            ? I18nManager.Instance.GetString("Pages.VersionManagePage.VersionType.UnknownClientVersion")
            : clientVersionId;
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
        var displayType = GetDisplayType(version);
        var (iconKind, iconPath) = ResolveVersionIcon(displayType, version);
        return new VersionRenderData
        {
            Version = version,
            DisplayType = displayType,
            Description = GetVersionDescription(version, displayType),
            IconKind = iconKind,
            IconPath = iconPath
        };
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
        catch (FileNotFoundException ex) when (Logger.DebugMode && IsMissingBuiltInVersionIcon(renderData, ex))
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
        return displayType switch
        {
            VersionDisplayType.Snapshot => "/Assets/VersionIcons/snapshot.png",
            VersionDisplayType.AprilFools => "/Assets/VersionIcons/aprilfools.png",
            VersionDisplayType.Old => "/Assets/VersionIcons/old.png",
            VersionDisplayType.Error => "/Assets/VersionIcons/error.png",
            _ => "/Assets/VersionIcons/release.png"
        };
    }

    private (VersionIconKind IconKind, string? IconPath) ResolveVersionIcon(
        VersionDisplayType displayType,
        LocalGameVersionEntry version)
    {
        if (displayType != VersionDisplayType.Error)
        {
            var customIconPath = _versionConfigManager.GetValue<string>(version, "iconPath");
            if (!string.IsNullOrWhiteSpace(customIconPath) && TryIsValidIconFile(customIconPath))
            {
                return (VersionIconKind.File, customIconPath);
            }
        }

        var assetPath = GetBuiltInIconResourcePath(displayType);
        return !string.IsNullOrWhiteSpace(assetPath)
            ? (VersionIconKind.Asset, assetPath)
            : (VersionIconKind.Symbol, null);
    }

    private bool TryIsValidIconFile(string path)
    {
        try
        {
            if (!File.Exists(path))
            {
                return false;
            }

            _ = new Uri(path);
            return true;
        }
        catch (Exception ex)
        {
            _logger.Warn($"Loading version icon failed: {ex.Message}");
            return false;
        }
    }

    private static bool IsMissingBuiltInVersionIcon(VersionRenderData renderData, FileNotFoundException exception)
    {
        return renderData.IconKind == VersionIconKind.Asset &&
               renderData.IconPath?.StartsWith("/Assets/VersionIcons/", StringComparison.OrdinalIgnoreCase) == true &&
               exception.Message.Contains("avares://LMCUI/Assets/VersionIcons/", StringComparison.OrdinalIgnoreCase);
    }

    private void NotifyInvalidVersions(IReadOnlyList<LocalGameVersionEntry> versions, bool forceInvalidNotification)
    {
        var invalidVersions = versions
            .Where(version => version.Status != VersionStatus.Valid)
            .Select(version => $"{version.VersionName} ({GetStatusText(version.Status)})")
            .ToList();

        if (invalidVersions.Count == 0)
        {
            _lastInvalidVersionSignature = string.Empty;
            return;
        }

        var signature = string.Join("|", invalidVersions);
        if (!forceInvalidNotification && string.Equals(signature, _lastInvalidVersionSignature, StringComparison.Ordinal))
        {
            return;
        }

        _lastInvalidVersionSignature = signature;
        _ = MessageQueueHelper.ShowWarning(
            I18nManager.Instance.GetString("Pages.VersionManagePage.InvalidVersions.Title"),
            I18nManager.Instance.GetString("Pages.VersionManagePage.InvalidVersions.Content", string.Join("，", invalidVersions)));
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
        if (_isPageLoaded)
        {
            RefreshPage();
        }
    }

    private void MainWindow_OnActivated(object? sender, EventArgs e)
    {
        if (!_isPageLoaded)
        {
            return;
        }

        if (_pendingExternalRefresh || DateTime.UtcNow - _lastRefreshUtc > TimeSpan.FromSeconds(2))
        {
            RefreshPage();
        }
    }

    private void ConfigureWatcher(string rootPath)
    {
        var normalizedRootPath = Path.GetFullPath(rootPath)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

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
        _pendingExternalRefresh = true;
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
