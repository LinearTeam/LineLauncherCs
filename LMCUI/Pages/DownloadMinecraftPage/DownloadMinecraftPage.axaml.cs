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
using Avalonia.Threading;
using FluentAvalonia.UI.Controls;
using LMC.Basic.Logging;
using LMC;
using LMCCore.Game.Download;
using LMCCore.Game.Download.Model.Vanilla;
using LMCCore.Game.Versioning;
using LMCCore.Game.Versioning.Discovery;
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
    private readonly ObservableCollection<ManifestVersionListItem> _visibleVersions = [];
    private readonly ObservableCollection<string> _searchCandidates = [];
    private readonly Dictionary<string, ManifestVersionListItem> _versionIdMap = [];
    private readonly FuncDataTemplate<ManifestVersionListItem> _versionItemTemplate;
    private IReadOnlyList<ManifestVersionListItem> _allVersions = [];
    private ManifestVersionListItem? _latestRelease;
    private ManifestVersionListItem? _latestSnapshot;
    private CancellationTokenSource? _loadCts;
    private bool _hasManifestLoaded;
    private bool _isManifestLoading;

    public DownloadMinecraftPage() : base("Pages.DownloadMinecraftPage.Title", "DownloadMinecraftPage")
    {
        _versionItemTemplate = new FuncDataTemplate<ManifestVersionListItem>((version, _) => CreateVersionExpander(version), true);
        InitializeComponent();
        SearchBox.ItemsSource = _searchCandidates;
        VersionListBox.ItemTemplate = _versionItemTemplate;
        VersionListBox.ItemsSource = _visibleVersions;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnLoaded(object? sender, RoutedEventArgs e)
    {
        if (_hasManifestLoaded)
        {
            UpdateLatestExpanders();
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

            UpdateLatestExpanders();
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
        ConfigureLatestExpander(
            LatestReleaseExpander,
            I18nManager.Instance.GetString("Pages.DownloadMinecraftPage.Latest.ReleaseHeader"),
            I18nManager.Instance.GetString("Pages.DownloadMinecraftPage.Loading"),
            string.Empty,
            false,
            null);
        ConfigureLatestExpander(
            LatestSnapshotExpander,
            I18nManager.Instance.GetString("Pages.DownloadMinecraftPage.Latest.SnapshotHeader"),
            I18nManager.Instance.GetString("Pages.DownloadMinecraftPage.Loading"),
            string.Empty,
            false,
            null);
    }

    private void UpdateLatestExpanders()
    {
        ConfigureLatestExpander(
            LatestReleaseExpander,
            I18nManager.Instance.GetString("Pages.DownloadMinecraftPage.Latest.ReleaseHeader"),
            _latestRelease?.Id ?? I18nManager.Instance.GetString("Pages.DownloadMinecraftPage.Empty.NoMatchingVersions"),
            _latestRelease?.Description ?? string.Empty,
            _latestRelease != null,
            _latestRelease);
        ConfigureLatestExpander(
            LatestSnapshotExpander,
            I18nManager.Instance.GetString("Pages.DownloadMinecraftPage.Latest.SnapshotHeader"),
            _latestSnapshot?.Id ?? I18nManager.Instance.GetString("Pages.DownloadMinecraftPage.Empty.NoMatchingVersions"),
            _latestSnapshot?.Description ?? string.Empty,
            _latestSnapshot != null,
            _latestSnapshot);
    }

    private void ConfigureLatestExpander(
        FASettingsExpander expander,
        string header,
        string contentText,
        string description,
        bool isClickable,
        ManifestVersionListItem? version)
    {
        expander.Header = header;
        expander.Description = description;
        expander.IsClickEnabled = isClickable;
        expander.IconSource = version == null ? CreateVersionIconSource(GameVersionDisplayType.Release) : CreateVersionIconSource(version.DisplayType);
        expander.Tag = version?.Id;
        expander.Footer = new TextBlock
        {
            Text = contentText,
            FontSize = 16,
            FontWeight = Avalonia.Media.FontWeight.SemiBold
        };
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
        var filteredVersions = GetFilteredVersions(SearchBox.Text ?? string.Empty);
        UpdateSearchCandidates(GetCandidateVersions());
        RenderVersions(filteredVersions);
    }

    private void SearchBox_OnTextChanged(object? sender, RoutedEventArgs e)
    {
        var filteredVersions = GetFilteredVersions(SearchBox.Text ?? string.Empty);
        RenderVersions(filteredVersions);
    }

    private void UpdateSearchCandidates(IReadOnlyList<ManifestVersionListItem> filteredVersions)
    {
        _searchCandidates.Clear();
        foreach (var candidateId in DownloadMinecraftPagePresentation.BuildSearchCandidates(filteredVersions))
        {
            _searchCandidates.Add(candidateId);
        }
    }

    private void RenderVersions(IReadOnlyList<ManifestVersionListItem> versions)
    {
        _visibleVersions.Clear();
        _versionIdMap.Clear();
        VersionListBox.ItemTemplate = _versionItemTemplate;
        VersionListBox.ItemsSource = _visibleVersions;

        if (versions.Count == 0)
        {
            RenderEmptyState(I18nManager.Instance.GetString("Pages.DownloadMinecraftPage.Empty.NoMatchingVersions"));
            return;
        }

        ShowVersionList();
        foreach (var version in versions)
        {
            _visibleVersions.Add(version);
            _versionIdMap[version.Id] = version;
        }
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
        VersionExpander_OnClick(sender, e);
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
                await _downloadManager.CreateDownloadPlanAsync(request);
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

        _ = dialog.ShowAsync();
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
            return new FABitmapIconSource
            {
                UriSource = new Uri($"avares://LMCUI{assetPath}")
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
        _visibleVersions.Clear();
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
}

