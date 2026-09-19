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
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using FluentAvalonia.UI.Controls;
using LMC.Basic.Logging;
using LMC;
using LMCCore.Game.Download;
using LMCCore.Game.Download.Model;
using LMCCore.Game.Download.Model.Vanilla;
using LMCCore.Game.Versioning;
using LMCCore.Game.Versioning.Discovery;
using LMCCore.Tasks.Model;
using LMCUI.I18n;
using LMCUI.Navigation.Model;
using LMCUI.Pages.DownloadMinecraftPage.DownloadMinecraft;
using LMCUI.Pages.TaskPage;
using LMCUI.Utils;

namespace LMCUI.Pages.DownloadMinecraftPage;

public partial class DownloadMinecraftPage : PageBase
{
    private readonly static TimeSpan MinimumDialogLoadingDuration = TimeSpan.FromMilliseconds(300);
    private readonly Logger _logger = new("DownloadMinecraftPage");
    private readonly DownloadManager _downloadManager = new();
    private IReadOnlyList<ManifestVersionListItem> _visibleVersions = [];
    private IReadOnlyList<string> _searchCandidates = [];
    private readonly Dictionary<string, ManifestVersionListItem> _versionIdMap = [];
    private readonly static Dictionary<string, IImage> s_versionIcons = [];
    private readonly FuncDataTemplate<ManifestVersionListItem> _versionItemTemplate;
    private IReadOnlyList<ManifestVersionListItem> _allVersions = [];
    private ManifestVersionListItem? _latestRelease;
    private ManifestVersionListItem? _latestSnapshot;
    private CancellationTokenSource? _loadCts;
    private DispatcherTimer? _searchDebounceTimer;
    private string _pendingSearchText = string.Empty;
    private bool _hasManifestLoaded;
    private bool _isManifestLoading;
    private bool _isViewInitialized;

    public DownloadMinecraftPage() : base("Pages.DownloadMinecraftPage.Title", "DownloadMinecraftPage")
    {
        _versionItemTemplate = new FuncDataTemplate<ManifestVersionListItem>(
            (version, _) => version == null ? null : CreateVersionExpander(version));
        InitializeComponent();
        _isViewInitialized = true;
        UpdateFilterButtonText();
        VersionListBox.ItemTemplate = _versionItemTemplate;
        VersionListBox.ItemsSource = _visibleVersions;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnLoaded(object? sender, RoutedEventArgs e)
    {
        if (_hasManifestLoaded)
        {
            UpdateLatestButtons();
            var filteredVersions = GetFilteredVersions(SearchBox.Text ?? string.Empty);
            UpdateSearchCandidates(GetCandidateVersions());
            RenderVersions(filteredVersions);
            return;
        }

        if (_isManifestLoading)
        {
            return;
        }

        _ = LoadManifestAsync();
    }

    private void OnUnloaded(object? sender, RoutedEventArgs e)
    {
        _loadCts?.Cancel();
        _searchDebounceTimer?.Stop();
    }

    async private Task LoadManifestAsync()
    {
        try
        {
            _isManifestLoading = true;
            _loadCts?.Cancel();
            _loadCts = new CancellationTokenSource();
            var cancellationToken = _loadCts.Token;

            ShowLoadingState(I18nManager.Instance.GetString("Pages.DownloadMinecraftPage.Loading"));
            SetLatestLoadingState();
            await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Render);

            VersionManifestInfo manifest;
            try
            {
                manifest = await _downloadManager.GetVersionManifestAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Loading version manifest");
                SetLatestUnavailableState(
                    I18nManager.Instance.GetString("Pages.DownloadMinecraftPage.Errors.LoadFailedTitle"));
                RenderEmptyState(I18nManager.Instance.GetString("Pages.DownloadMinecraftPage.Errors.LoadFailedContent", ex.Message));
                await MessageQueueHelper.ShowError(
                    I18nManager.Instance.GetString("Pages.DownloadMinecraftPage.Errors.LoadFailedTitle"),
                    I18nManager.Instance.GetString("Pages.DownloadMinecraftPage.Errors.LoadFailedContent", ex.Message));
                return;
            }

            if (cancellationToken.IsCancellationRequested)
            {
                return;
            }

            var manifestState = DownloadMinecraftPagePresentation.BuildManifestState(manifest, GetDisplayTypeText);
            _allVersions = manifestState.Versions;
            _latestRelease = manifestState.LatestRelease;
            _latestSnapshot = manifestState.LatestSnapshot;
            _hasManifestLoaded = true;

            UpdateLatestButtons();
            UpdateSearchCandidates(GetFilteredVersions(string.Empty));
            RenderVersions(GetFilteredVersions(SearchBox.Text ?? string.Empty));
        }
        finally
        {
            _isManifestLoading = false;
        }
    }

    private void SetLatestLoadingState()
    {
        SetLatestUnavailableState(I18nManager.Instance.GetString("Pages.DownloadMinecraftPage.Loading"));
    }

    private void SetLatestUnavailableState(string text)
    {
        ConfigureLatestExpander(LatestReleaseExpander, LatestReleaseVersionText, text, null);
        ConfigureLatestExpander(LatestSnapshotExpander, LatestSnapshotVersionText, text, null);
    }

    private void UpdateLatestButtons()
    {
        var unavailableText = I18nManager.Instance.GetString("Pages.DownloadMinecraftPage.Empty.NoMatchingVersions");
        ConfigureLatestExpander(LatestReleaseExpander, LatestReleaseVersionText,
            _latestRelease?.Id ?? unavailableText, _latestRelease);
        ConfigureLatestExpander(LatestSnapshotExpander, LatestSnapshotVersionText,
            _latestSnapshot?.Id ?? unavailableText, _latestSnapshot);
    }

    private void ConfigureLatestExpander(
        FASettingsExpander expander,
        TextBlock versionText,
        string text,
        ManifestVersionListItem? version)
    {
        versionText.Text = text;
        expander.Description = version?.LocalReleaseTimeText ?? string.Empty;
        expander.IsClickEnabled = version != null;
        expander.IconSource = CreateVersionIconSource(version?.DisplayType ?? GameVersionDisplayType.Release);
        expander.Tag = version;
    }

    private DownloadMinecraftFilterOptions GetFilterOptions()
    {
        return new DownloadMinecraftFilterOptions(
            ReleaseFilterCheckBox.IsChecked == true,
            SnapshotFilterCheckBox.IsChecked == true,
            AprilFoolsFilterCheckBox.IsChecked == true,
            OldFilterCheckBox.IsChecked == true);
    }

    private IReadOnlyList<ManifestVersionListItem> GetFilteredVersions(string searchText)
    {
        return DownloadMinecraftPagePresentation.FilterVersions(_allVersions, GetFilterOptions(), searchText);
    }

    private IReadOnlyList<ManifestVersionListItem> GetCandidateVersions()
    {
        return DownloadMinecraftPagePresentation.FilterVersions(_allVersions, GetFilterOptions(), null);
    }

    private void FilterCheckBox_OnChanged(object? sender, RoutedEventArgs e)
    {
        if (!_isViewInitialized)
            return;

        UpdateFilterButtonText();
        var filteredVersions = GetFilteredVersions(SearchBox.Text ?? string.Empty);
        UpdateSearchCandidates(GetCandidateVersions());
        RenderVersions(filteredVersions);
    }

    private void SearchBox_OnTextChanged(object? sender, RoutedEventArgs e)
    {
        _pendingSearchText = SearchBox.Text ?? string.Empty;
        EnsureSearchDebounceTimer();
        _searchDebounceTimer?.Stop();
        _searchDebounceTimer?.Start();
    }

    private void UpdateFilterButtonText()
    {
        var isDefault = ReleaseFilterCheckBox.IsChecked == true &&
                        SnapshotFilterCheckBox.IsChecked != true &&
                        AprilFoolsFilterCheckBox.IsChecked != true &&
                        OldFilterCheckBox.IsChecked != true;
        if (isDefault)
        {
            FilterButtonText.Text = I18nManager.Instance.GetString("Pages.DownloadMinecraftPage.Filters.Title");
            return;
        }

        var selectedCount = new[]
        {
            ReleaseFilterCheckBox.IsChecked == true,
            SnapshotFilterCheckBox.IsChecked == true,
            AprilFoolsFilterCheckBox.IsChecked == true,
            OldFilterCheckBox.IsChecked == true
        }.Count(selected => selected);
        FilterButtonText.Text = I18nManager.Instance.GetString(
            "Pages.DownloadMinecraftPage.Filters.ActiveCount", selectedCount);
    }

    private void UpdateSearchCandidates(IReadOnlyList<ManifestVersionListItem> filteredVersions)
    {
        _searchCandidates = DownloadMinecraftPagePresentation.BuildSearchCandidates(filteredVersions);
        SearchBox.ItemsSource = _searchCandidates;
    }

    private void RenderVersions(IReadOnlyList<ManifestVersionListItem> versions)
    {
        _versionIdMap.Clear();

        if (versions.Count == 0)
        {
            RenderEmptyState(I18nManager.Instance.GetString("Pages.DownloadMinecraftPage.Empty.NoMatchingVersions"));
            return;
        }

        ShowVersionList();
        foreach (var version in versions)
        {
            _versionIdMap[version.Id] = version;
        }

        _visibleVersions = versions;
        VersionListBox.ItemsSource = _visibleVersions;
    }

    private FASettingsExpander CreateVersionExpander(ManifestVersionListItem version)
    {
        var expander = new FASettingsExpander
        {
            Header = version.Id,
            Description = version.Description,
            IsClickEnabled = true,
            Focusable = false,
            ActionIconSource = new FASymbolIconSource
            {
                Symbol = FASymbol.ChevronRight
            },
            IconSource = CreateVersionIconSource(version.DisplayType),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Tag = version.Id
        };
        expander.Click += VersionExpander_OnClick;
        return expander;
    }

    private void VersionExpander_OnClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not FASettingsExpander { Tag: string versionId }) return;
        if(!_versionIdMap.TryGetValue(versionId, out var version))
        {
            if(_latestSnapshot?.Id == versionId) ShowVersionDialog(_latestSnapshot);
            return;
        }
        
        ShowVersionDialog(version);
    }

    private void LatestVersionExpander_OnClick(object? sender, RoutedEventArgs e)
    {
        if (sender is FASettingsExpander { Tag: ManifestVersionListItem version })
            ShowVersionDialog(version);
    }

    private void LatestVersionsGrid_OnSizeChanged(object? sender, SizeChangedEventArgs e)
    {
        var stacked = e.NewSize.Width < 620;
        if (sender is Grid grid)
        {
            grid.RowSpacing = stacked ? 8 : 0;
            grid.ColumnSpacing = stacked ? 0 : 8;
        }
        Grid.SetColumnSpan(LatestReleaseExpander, stacked ? 2 : 1);
        Grid.SetColumn(LatestSnapshotExpander, stacked ? 0 : 1);
        Grid.SetColumnSpan(LatestSnapshotExpander, stacked ? 2 : 1);
        Grid.SetRow(LatestSnapshotExpander, stacked ? 1 : 0);
    }

    private void ShowVersionDialog(ManifestVersionListItem version)
    {
        var wizardContext = DownloadMinecraftPagePresentation.TryCreateWizardContext(Current.Config.SelectedGameRootPath, version);
        if (wizardContext == null)
        {
            _ = MessageQueueHelper.ShowError(
                I18nManager.Instance.GetString("Pages.DownloadMinecraftPage.Wizard.Errors.NoRootTitle"),
                I18nManager.Instance.GetString("Pages.DownloadMinecraftPage.Wizard.Errors.NoRootContent"));
            return;
        }

        var dialog = new FAContentDialog
        {
            Title = I18nManager.Instance.GetString("Pages.DownloadMinecraftPage.Wizard.Title", version.Id),
            CloseButtonText = I18nManager.Instance.GetString("Pages.DownloadMinecraftPage.Dialog.CloseButton"),
            PrimaryButtonText = I18nManager.Instance.GetString("Pages.DownloadMinecraftPage.Wizard.PreviousButton"),
            SecondaryButtonText = I18nManager.Instance.GetString("Pages.DownloadMinecraftPage.Wizard.FinishButton"),
            DefaultButton = FAContentDialogButton.Secondary,
            IsPrimaryButtonEnabled = false,
            IsSecondaryButtonEnabled = true
        };
        var isDialogBusy = false;
        var allowProgrammaticClose = false;

        var wizard = new DownloadMinecraft.DownloadMinecraftWizard(
            wizardContext,
            state =>
            {
                dialog.IsPrimaryButtonEnabled = !isDialogBusy && state.hasPrev;
                dialog.IsSecondaryButtonEnabled = !isDialogBusy && state.hasNext;
                dialog.SecondaryButtonText = state.isFinal
                    ? I18nManager.Instance.GetString("Pages.DownloadMinecraftPage.Wizard.FinishButton")
                    : I18nManager.Instance.GetString("Pages.DownloadMinecraftPage.Wizard.NextButton");
            });

        dialog.Content = wizard;
        dialog.PrimaryButtonClick += (s, e) =>
        {
            if (isDialogBusy)
            {
                e.Cancel = true;
                return;
            }

            e.Cancel = true;
            wizard.PreviousStep(s, e);
        };
        dialog.SecondaryButtonClick += async (_, e) =>
        {
            e.Cancel = true;
            if (!wizard.Continue())
            {
                return;
            }

            var selection = wizard.Result;
            if (selection == null)
            {
                return;
            }

            isDialogBusy = true;
            dialog.IsPrimaryButtonEnabled = false;
            dialog.IsSecondaryButtonEnabled = false;
            dialog.CloseButtonText = string.Empty;
            dialog.Content = CreateDialogLoadingContent(
                I18nManager.Instance.GetString("Pages.DownloadMinecraftPage.Loading"));
            var startTime = DateTime.UtcNow;

            try
            {
                var request = DownloadMinecraftWizardSupport.CreateDownloadRequest(selection);
                var plan = await _downloadManager.CreateDownloadPlanAsync(request);
                ObserveInstallationCompletion(plan);
                await EnsureMinimumDialogLoadingDurationAsync(startTime);

                isDialogBusy = false;
                allowProgrammaticClose = true;
                dialog.Hide();
                NavigateToTaskPage();
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Creating download plan");
                await EnsureMinimumDialogLoadingDurationAsync(startTime);
                await MessageQueueHelper.ShowError(
                    I18nManager.Instance.GetString("Pages.DownloadMinecraftPage.Errors.LoadFailedTitle"),
                    ex.Message);
                isDialogBusy = false;
                dialog.Content = wizard;
                dialog.IsPrimaryButtonEnabled = true;
                dialog.CloseButtonText = I18nManager.Instance.GetString("Pages.DownloadMinecraftPage.Dialog.CloseButton");
                dialog.IsSecondaryButtonEnabled = true;
            }
        };
        dialog.CloseButtonClick += (_, args) =>
        {
            if (DownloadMinecraftWizardSupport.ShouldCancelDialogClose(isDialogBusy, allowProgrammaticClose))
            {
                args.Cancel = true;
            }
        };
        dialog.Closing += (_, args) =>
        {
            if (DownloadMinecraftWizardSupport.ShouldCancelDialogClose(isDialogBusy, allowProgrammaticClose))
            {
                args.Cancel = true;
            }
        };

        _ = ShowWizardDialogAsync(dialog, wizard);
    }

    private async Task ShowWizardDialogAsync(FAContentDialog dialog, DownloadMinecraft.DownloadMinecraftWizard wizard)
    {
        try
        {
            await dialog.ShowAsync();
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Showing download wizard");
        }
        finally
        {
            wizard.CancelPendingOperations();
        }
    }

    private void ObserveInstallationCompletion(DownloadGamePlan plan)
    {
        var finalizationTask = plan.FinalizationTask;
        if (finalizationTask == null)
        {
            _logger.Warn($"Installation plan has no finalization task: {plan.Request.VersionName}");
            return;
        }

        var handled = 0;
        void OnCompleted(SubTaskBase task)
        {
            if (Interlocked.Exchange(ref handled, 1) != 0)
                return;

            finalizationTask.Completed -= OnCompleted;
            if (task.State == TaskState.Completed)
                _ = RefreshInstalledVersionsAsync(plan.Request.RootPath);
        }

        finalizationTask.Completed += OnCompleted;
        if (finalizationTask.IsFinished)
            OnCompleted(finalizationTask);
    }

    private async Task RefreshInstalledVersionsAsync(string rootPath)
    {
        try
        {
            var versions = await new VersionManager().RefreshVersionsAsync(rootPath);
            await Dispatcher.UIThread.InvokeAsync(() =>
                VersionCatalogRefreshCoordinator.PublishInstalledVersion(rootPath, versions));
        }
        catch (Exception ex)
        {
            _logger.Error(ex, $"Refreshing installed versions in {rootPath}");
        }
    }

    async private static Task EnsureMinimumDialogLoadingDurationAsync(DateTime startTime)
    {
        var elapsed = DateTime.UtcNow - startTime;
        var remaining = MinimumDialogLoadingDuration - elapsed;
        if (remaining > TimeSpan.Zero)
        {
            await Task.Delay(remaining);
        }
    }

    private static Control CreateDialogLoadingContent(string text)
    {
        return new Grid
        {
            MinHeight = 220,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Center,
            Children =
            {
                new StackPanel
                {
                    Orientation = Orientation.Vertical,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    Spacing = 12,
                    Children =
                    {
                        new FAProgressRing
                        {
                            IsActive = true,
                            IsIndeterminate = true,
                            Width = 36,
                            Height = 36
                        },
                        new TextBlock
                        {
                            Text = text,
                            HorizontalAlignment = HorizontalAlignment.Center
                        }
                    }
                }
            }
        };
    }

    private static void NavigateToTaskPage()
    {
        var selectedItem = MainWindow.Instance.mnv.SelectedItem as FANavigationViewItem
                           ?? MainWindow.Instance.mnv.FooterMenuItems.OfType<FANavigationViewItem>()
                               .First(item => string.Equals(item.Tag?.ToString(), "TaskPage", StringComparison.Ordinal));

        MainWindow.NavigatePage(
            new PageNavigateWay(typeof(TaskPage.TaskPage), selectedItem),
            NavigateType.New);
    }

    private string GetDisplayTypeText(GameVersionDisplayType displayType)
    {
        return displayType switch
        {
            GameVersionDisplayType.Release => I18nManager.Instance.GetString("Pages.DownloadMinecraftPage.Types.Release"),
            GameVersionDisplayType.Snapshot => I18nManager.Instance.GetString("Pages.DownloadMinecraftPage.Types.Snapshot"),
            GameVersionDisplayType.AprilFools => I18nManager.Instance.GetString("Pages.DownloadMinecraftPage.Types.AprilFools"),
            GameVersionDisplayType.Old => I18nManager.Instance.GetString("Pages.DownloadMinecraftPage.Types.Old"),
            _ => I18nManager.Instance.GetString("Pages.DownloadMinecraftPage.Types.Release")
        };
    }

    private FAIconSource CreateVersionIconSource(GameVersionDisplayType displayType)
    {
        var assetPath = DownloadMinecraftPagePresentation.GetBuiltInIconResourcePath(displayType);

        try
        {
            return new FAImageIconSource
            {
                Source = GetVersionIcon(assetPath)
            };
        }
        catch (FileNotFoundException ex) when (IsMissingBuiltInVersionIcon(assetPath, ex))
        {
            _logger.Debug($"Ignored missing built-in version icon resource: {assetPath}");
            return CreateFallbackIconSource();
        }
    }

    private static FAIconSource CreateFallbackIconSource()
    {
        return new FASymbolIconSource
        {
            Symbol = FASymbol.Games
        };
    }

    private static IImage GetVersionIcon(string assetPath)
    {
        if (s_versionIcons.TryGetValue(assetPath, out var icon))
        {
            return icon;
        }

        using var stream = AssetLoader.Open(new Uri($"avares://LMCUI{assetPath}"));
        // Built-in version icons are 500px images with broad transparent margins.
        icon = new CroppedBitmap(new Bitmap(stream), new PixelRect(140, 140, 220, 220));
        s_versionIcons[assetPath] = icon;
        return icon;
    }

    private static bool IsMissingBuiltInVersionIcon(string assetPath, FileNotFoundException exception)
    {
        return assetPath.StartsWith("/Assets/VersionIcons/", StringComparison.OrdinalIgnoreCase) &&
               exception.Message.Contains("avares://LMCUI/Assets/VersionIcons/", StringComparison.OrdinalIgnoreCase);
    }

    private void ShowLoadingState(string text)
    {
        ShowStateContent(CreateCenteredProgressContainer(text));
    }

    private void RenderEmptyState(string text)
    {
        _visibleVersions = [];
        VersionListBox.ItemsSource = _visibleVersions;
        _versionIdMap.Clear();
        ShowStateContent(new TextBlock
        {
            Text = text,
            TextWrapping = Avalonia.Media.TextWrapping.Wrap,
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

    private void ShowVersionList()
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

    private void EnsureSearchDebounceTimer()
    {
        _searchDebounceTimer ??= new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(150)
        };
        _searchDebounceTimer.Tick -= SearchDebounceTimer_OnTick;
        _searchDebounceTimer.Tick += SearchDebounceTimer_OnTick;
    }

    private void SearchDebounceTimer_OnTick(object? sender, EventArgs e)
    {
        _searchDebounceTimer?.Stop();
        RenderVersions(GetFilteredVersions(_pendingSearchText));
    }
}

