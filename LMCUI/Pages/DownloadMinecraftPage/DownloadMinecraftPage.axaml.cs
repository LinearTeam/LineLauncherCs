using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
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
using LMCCore.Game.Download;
using LMCCore.Game.Download.Model.Vanilla;
using LMCCore.Game.Versioning;
using LMCUI.I18n;
using LMCUI.Utils;

namespace LMCUI.Pages.DownloadMinecraftPage;

public partial class DownloadMinecraftPage : PageBase
{
    private readonly Logger _logger = new("DownloadMinecraftPage");
    private readonly DownloadManager _downloadManager = new();
    private readonly ObservableCollection<ManifestVersionViewModel> _visibleVersions = [];
    private readonly ObservableCollection<string> _searchCandidates = [];
    private readonly Dictionary<string, ManifestVersionViewModel> _versionIdMap = [];
    private readonly FuncDataTemplate<ManifestVersionViewModel> _versionItemTemplate;
    private IReadOnlyList<ManifestVersionViewModel> _allVersions = [];
    private ManifestVersionViewModel? _latestRelease;
    private ManifestVersionViewModel? _latestSnapshot;
    private CancellationTokenSource? _loadCts;
    private bool _hasManifestLoaded;
    private bool _isManifestLoading;

    private sealed class ManifestVersionViewModel
    {
        public required string Id { get; init; }
        public required GameVersionDisplayType DisplayType { get; init; }
        public required string DisplayTypeText { get; init; }
        public required string LocalReleaseTimeText { get; init; }
        public required string Description { get; init; }
        public required VersionEntry Source { get; init; }
    }

    public DownloadMinecraftPage() : base("Pages.DownloadMinecraftPage.Title", "DownloadMinecraftPage")
    {
        _versionItemTemplate = new FuncDataTemplate<ManifestVersionViewModel>((version, _) => CreateVersionExpander(version), true);
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

            var viewModels = manifest.Versions
                .Select(CreateVersionViewModel)
                .OrderByDescending(item => item.Source.ReleaseTime)
                .ToList();

            _allVersions = viewModels;
            _latestRelease = viewModels.FirstOrDefault(item => string.Equals(item.Id, manifest.Latest.Release, StringComparison.OrdinalIgnoreCase));
            _latestSnapshot = viewModels.FirstOrDefault(item => string.Equals(item.Id, manifest.Latest.Snapshot, StringComparison.OrdinalIgnoreCase));
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

    private ManifestVersionViewModel CreateVersionViewModel(VersionEntry version)
    {
        var displayType = GameVersionTypeClassifier.ClassifyManifestVersion(version);
        var displayTypeText = GetDisplayTypeText(displayType);
        var localReleaseTimeText = version.ReleaseTime.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.CurrentCulture);
        return new ManifestVersionViewModel
        {
            Id = version.Id,
            DisplayType = displayType,
            DisplayTypeText = displayTypeText,
            LocalReleaseTimeText = localReleaseTimeText,
            Description = $"{displayTypeText} | {localReleaseTimeText}",
            Source = version
        };
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
        ManifestVersionViewModel? version)
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

    private IReadOnlyList<ManifestVersionViewModel> GetFilteredVersions(string searchText)
    {
        var normalizedSearch = searchText.Trim();
        return _allVersions
            .Where(IsVisibleByFilter)
            .Where(item => string.IsNullOrWhiteSpace(normalizedSearch) ||
                           item.Id.Contains(normalizedSearch, StringComparison.OrdinalIgnoreCase))
            .ToList();
    }

    private IReadOnlyList<ManifestVersionViewModel> GetCandidateVersions()
    {
        return _allVersions
            .Where(IsVisibleByFilter)
            .ToList();
    }

    private bool IsVisibleByFilter(ManifestVersionViewModel item)
    {
        return item.DisplayType switch
        {
            GameVersionDisplayType.Release => ReleaseFilterCheckBox.IsChecked == true,
            GameVersionDisplayType.Snapshot => SnapshotFilterCheckBox.IsChecked == true,
            GameVersionDisplayType.AprilFools => AprilFoolsFilterCheckBox.IsChecked == true,
            GameVersionDisplayType.Old => OldFilterCheckBox.IsChecked == true,
            _ => true
        };
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

    private void UpdateSearchCandidates(IReadOnlyList<ManifestVersionViewModel> filteredVersions)
    {
        var candidateIds = filteredVersions
            .Select(item => item.Id)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        _searchCandidates.Clear();
        foreach (var candidateId in candidateIds)
        {
            _searchCandidates.Add(candidateId);
        }
    }

    private void RenderVersions(IReadOnlyList<ManifestVersionViewModel> versions)
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

    private FASettingsExpander CreateVersionExpander(ManifestVersionViewModel version)
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
        if (sender is not FASettingsExpander { Tag: string versionId } || !_versionIdMap.TryGetValue(versionId, out var version))
            return;

        ShowVersionDialog(version);
    }

    private void LatestVersionExpander_OnClick(object? sender, RoutedEventArgs e)
    {
        VersionExpander_OnClick(sender, e);
    }

    private void ShowVersionDialog(ManifestVersionViewModel version)
    {
        var dialog = new FAContentDialog
        {
            Title = I18nManager.Instance.GetString("Pages.DownloadMinecraftPage.Dialog.Title", version.Id),
            CloseButtonText = I18nManager.Instance.GetString("Pages.DownloadMinecraftPage.Dialog.CloseButton"),
            Content = new StackPanel
            {
                Spacing = 8,
                Children =
                {
                    new TextBlock
                    {
                        Text = I18nManager.Instance.GetString("Pages.DownloadMinecraftPage.Dialog.VersionId", version.Id),
                        TextWrapping = Avalonia.Media.TextWrapping.Wrap
                    },
                    new TextBlock
                    {
                        Text = I18nManager.Instance.GetString("Pages.DownloadMinecraftPage.Dialog.VersionType", version.DisplayTypeText),
                        TextWrapping = Avalonia.Media.TextWrapping.Wrap
                    },
                    new TextBlock
                    {
                        Text = I18nManager.Instance.GetString("Pages.DownloadMinecraftPage.Dialog.ReleaseTime", version.LocalReleaseTimeText),
                        TextWrapping = Avalonia.Media.TextWrapping.Wrap
                    },
                    new TextBlock
                    {
                        Text = I18nManager.Instance.GetString("Pages.DownloadMinecraftPage.Dialog.Placeholder"),
                        TextWrapping = Avalonia.Media.TextWrapping.Wrap,
                        Opacity = 0.8,
                        Margin = new Thickness(0, 8, 0, 0)
                    }
                }
            }
        };
        dialog.ShowAsync();
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
        var assetPath = GetBuiltInIconResourcePath(displayType);

        try
        {
            return new FABitmapIconSource
            {
                UriSource = new Uri($"avares://LMCUI{assetPath}")
            };
        }
        catch (FileNotFoundException ex) when (Logger.DebugMode && IsMissingBuiltInVersionIcon(assetPath, ex))
        {
            _logger.Debug($"Ignored missing built-in version icon resource: {assetPath}");
            return CreateFallbackIconSource();
        }
    }

    private static string GetBuiltInIconResourcePath(GameVersionDisplayType displayType)
    {
        return displayType switch
        {
            GameVersionDisplayType.Snapshot => "/Assets/VersionIcons/snapshot.png",
            GameVersionDisplayType.AprilFools => "/Assets/VersionIcons/aprilfools.png",
            GameVersionDisplayType.Old => "/Assets/VersionIcons/old.png",
            _ => "/Assets/VersionIcons/release.png"
        };
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
