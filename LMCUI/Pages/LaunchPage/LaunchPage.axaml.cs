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
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using Avalonia.VisualTree;
using FluentAvalonia.UI.Controls;
using LMC;
using LMC.Basic.Configs;
using LMC.Basic.Logging;
using LMCCore.Account;
using LMCCore.Account.Model;
using LMCCore.Game.Launching;
using LMCCore.Game.Model;
using LMCCore.Game.Versioning;
using LMCCore.Game.Versioning.Discovery;
using LMCUI.I18n;
using LMCUI.Navigation;
using LMCUI.Navigation.Model;
using LMCUI.Pages;
using LMCUI.Pages.AccountPage;
using LMCUI.Utils;
using AccountManagementPage = LMCUI.Pages.AccountPage.AccountPage;
using DownloadMinecraftPageContent = LMCUI.Pages.DownloadMinecraftPage.DownloadMinecraftPage;
using VersionManagementPage = LMCUI.Pages.VersionManagePage.VersionManagePage;

namespace LMCUI.Pages.LaunchPage;

public partial class LaunchPage : PageBase
{
    private readonly static IReadOnlyDictionary<GameVersionDisplayType, IImage> s_versionIcons =
        new Dictionary<GameVersionDisplayType, IImage>
        {
            [GameVersionDisplayType.Release] = CreateVersionIcon("/Assets/VersionIcons/release.png"),
            [GameVersionDisplayType.Snapshot] = CreateVersionIcon("/Assets/VersionIcons/snapshot.png"),
            [GameVersionDisplayType.AprilFools] = CreateVersionIcon("/Assets/VersionIcons/aprilfools.png"),
            [GameVersionDisplayType.Old] = CreateVersionIcon("/Assets/VersionIcons/old.png")
        };
    private readonly static ConcurrentDictionary<string, IImage> s_accountAvatarImages = new(StringComparer.Ordinal);
    private const double VersionSelectionPanelHorizontalChrome = 112;
    private const double AccountSelectionPanelHorizontalChrome = 82;

    private readonly Logger _logger = new("LaunchPage");
    private readonly VersionManager _versionManager = new();
    private IReadOnlyList<LaunchVersionOption> _versionOptions = [];
    private IReadOnlyList<LaunchAccountOption> _accountOptions = [];
    private bool _areAccountsLoading;
    private bool _areVersionsLoading;
    private bool _isVersionCatalogLoading;
    private bool _isUpdatingVersionSelection;
    private bool _isLaunching;
    private bool _isPageLoaded;
    private int _accountLoadRequestId;
    private int _versionLoadRequestId;
    private int _accountAvatarAnimationRequestId;
    private LaunchVersionOption? _selectedVersion;
    private LaunchAccountOption? _selectedAccount;
    private GameLaunchResult? _runningGame;
    private bool _isTerminatingGame;

    public LaunchPage() : base("Pages.LaunchPage.Title", "LaunchPage")
    {
        InitializeComponent();
        VersionSelectionPopup.PlacementTarget = VersionSelectorButton;
        AccountSelectionPopup.PlacementTarget = AccountButton;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnLoaded(object? sender, RoutedEventArgs e)
    {
        _isPageLoaded = true;
        VersionCatalogRefreshCoordinator.Refreshed += VersionCatalogRefreshCoordinator_OnRefreshed;
        _ = LoadLaunchOptionsAsync(VersionCatalogRefreshCoordinator.LatestSnapshot);
    }

    private void OnUnloaded(object? sender, RoutedEventArgs e)
    {
        _isPageLoaded = false;
        _accountLoadRequestId++;
        _versionLoadRequestId++;
        SetVersionCatalogLoading(false);
        VersionCatalogRefreshCoordinator.Refreshed -= VersionCatalogRefreshCoordinator_OnRefreshed;
        VersionSelectionPopup.IsOpen = false;
        AccountSelectionPopup.IsOpen = false;
    }

    async private Task LoadLaunchOptionsAsync(VersionCatalogSnapshot? snapshot = null)
    {
        await Task.WhenAll(
            LoadAccountsAsync(),
            LoadVersionsAsync(snapshot));
    }

    async private Task LoadAccountsAsync()
    {
        var loadRequestId = ++_accountLoadRequestId;
        _areAccountsLoading = true;
        UpdateAccountPresentation();
        UpdateLaunchButtonState();

        try
        {
            var accountLoadItems = await Task.Run(() =>
            {
                AccountManager.Load();
                var loadedAccounts = AccountManager.Accounts.ToList();
                foreach (var account in loadedAccounts)
                {
                    AccountAvatarService.ApplyCachedOrDefaultAvatar(account);
                }

                return (IReadOnlyList<LaunchAccountLoadItem>)loadedAccounts
                    .Select(account => new LaunchAccountLoadItem(account, LoadAccountAvatar(account)))
                    .ToList();
            });

            if (_isPageLoaded && loadRequestId == _accountLoadRequestId)
            {
                _accountOptions = accountLoadItems
                    .Select(account => CreateAccountOption(account.Account, account.Avatar))
                    .ToList()
                    .AsReadOnly();
                _selectedAccount = _accountOptions.FirstOrDefault(option =>
                                       string.Equals(
                                           option.PersistentKey,
                                           Current.Config.SelectedLaunchAccountKey,
                                           StringComparison.Ordinal))
                                   ?? _accountOptions.FirstOrDefault();
                AccountSelectionListBox.ItemsSource = _accountOptions;
                AccountSelectionListBox.SelectedItem = _selectedAccount;
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Loading launch page accounts");
            if (_isPageLoaded && loadRequestId == _accountLoadRequestId)
            {
                await MessageQueueHelper.ShowError(
                    I18nManager.Instance.GetString("Pages.LaunchPage.LaunchDialog.Title"),
                    I18nManager.Instance.GetString("Pages.LaunchPage.LoadFailed", ex.Message));
            }
        }
        finally
        {
            if (_isPageLoaded && loadRequestId == _accountLoadRequestId)
            {
                _areAccountsLoading = false;
                UpdateAccountPresentation();
                UpdateLaunchButtonState();
            }
        }
    }

    async private Task LoadVersionsAsync(VersionCatalogSnapshot? snapshot)
    {
        var loadRequestId = ++_versionLoadRequestId;
        _areVersionsLoading = true;
        SetVersionCatalogLoading(false);
        UpdateLaunchButtonState();

        try
        {
            var selectedRoot = _versionManager.GetSelectedRoot();
            var hasCurrentSnapshot = snapshot != null &&
                                     string.Equals(
                                         snapshot.RootPath,
                                         selectedRoot?.RootPath,
                                         StringComparison.OrdinalIgnoreCase);
            if (hasCurrentSnapshot)
            {
                ApplyVersionCatalog(
                    snapshot!.Versions,
                    _versionManager.GetSelectedLaunchVersionName(selectedRoot!.RootPath));
                return;
            }

            if (_versionManager.TryGetCachedSelectedRootVersions(out var cachedVersions))
            {
                ApplyVersionCatalog(
                    cachedVersions,
                    selectedRoot == null
                        ? string.Empty
                        : _versionManager.GetSelectedLaunchVersionName(selectedRoot.RootPath));
                return;
            }

            var selectedVersion = await Task.Run(
                () => _versionManager.ResolveSelectedLaunchVersionAsync());
            if (_isPageLoaded && loadRequestId == _versionLoadRequestId && selectedVersion != null)
            {
                ApplyVersionCatalog([selectedVersion], selectedVersion.VersionName);
                _areVersionsLoading = false;
                SetVersionCatalogLoading(true);
                UpdateLaunchButtonState();
                _ = LoadCompleteVersionCatalogAsync(loadRequestId, selectedVersion.RootPath);
                return;
            }

            if (_isPageLoaded && loadRequestId == _versionLoadRequestId)
            {
                // All local candidates are missing or malformed.  Do not make the startup UI wait
                // for the metadata scan (which can include a network manifest request) before
                // offering the normal download path.
                ApplyVersionCatalog([], string.Empty);
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Loading launch page versions");
            if (_isPageLoaded && loadRequestId == _versionLoadRequestId)
            {
                await MessageQueueHelper.ShowError(
                    I18nManager.Instance.GetString("Pages.LaunchPage.LaunchDialog.Title"),
                    I18nManager.Instance.GetString("Pages.LaunchPage.LoadFailed", ex.Message));
            }
        }
        finally
        {
            if (_isPageLoaded && loadRequestId == _versionLoadRequestId)
            {
                _areVersionsLoading = false;
                UpdateLaunchButtonState();
            }
        }
    }

    async private Task LoadCompleteVersionCatalogAsync(int loadRequestId, string rootPath)
    {
        try
        {
            var versions = await _versionManager.GetCachedOrScanVersionsAsync(rootPath);
            if (_isPageLoaded && loadRequestId == _versionLoadRequestId)
            {
                ApplyVersionCatalog(
                    versions,
                    _versionManager.GetSelectedLaunchVersionName(rootPath));
            }
        }
        catch (Exception ex)
        {
            _logger.Warn($"Refreshing the launch page version catalog: {ex.Message}");
        }
        finally
        {
            if (_isPageLoaded && loadRequestId == _versionLoadRequestId)
            {
                SetVersionCatalogLoading(false);
            }
        }
    }

    private void SetVersionCatalogLoading(bool isLoading)
    {
        _isVersionCatalogLoading = isLoading;
        VersionSelectionLoadingIndicator.IsVisible = isLoading;
        VersionSelectionLoadingRing.IsActive = isLoading;
        if (VersionSelectionPopup.IsOpen)
        {
            UpdateVersionSelectionPanelWidth();
        }
    }

    private void ApplyVersionCatalog(
        IReadOnlyList<LocalGameVersionEntry> versions,
        string selectedVersionName)
    {
        _versionOptions = versions
            .Select(CreateVersionOption)
            .ToList()
            .AsReadOnly();
        _selectedVersion = _versionOptions.FirstOrDefault(option =>
                               string.Equals(
                                   option.VersionName,
                                   selectedVersionName,
                                   StringComparison.Ordinal) &&
                               IsLaunchable(option.Version))
                           ?? _versionOptions.FirstOrDefault(option => IsLaunchable(option.Version));
        if (_selectedVersion != null &&
            !string.Equals(_selectedVersion.VersionName, selectedVersionName, StringComparison.Ordinal))
        {
            try
            {
                _versionManager.SetSelectedLaunchVersionName(
                    _selectedVersion.Version.RootPath,
                    _selectedVersion.VersionName);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Saving fallback launch version");
            }
        }

        _isUpdatingVersionSelection = true;
        try
        {
            VersionSelectionListBox.ItemsSource = _versionOptions;
            VersionSelectionListBox.SelectedItem = _selectedVersion;
        }
        finally
        {
            _isUpdatingVersionSelection = false;
        }

        if (VersionSelectionPopup.IsOpen && !_isVersionCatalogLoading)
        {
            UpdateVersionSelectionPanelWidth();
        }
    }

    private static bool IsLaunchable(LocalGameVersionEntry version)
    {
        return version is { Status: VersionStatus.Valid, VersionInfo: not null };
    }

    private void VersionCatalogRefreshCoordinator_OnRefreshed(VersionCatalogSnapshot snapshot)
    {
        PostToUi(() => _ = LoadVersionsAsync(snapshot));
    }

    private void AccountButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (_isLaunching || _areAccountsLoading)
        {
            return;
        }

        if (_accountOptions.Count > 0)
        {
            var shouldOpen = !AccountSelectionPopup.IsOpen;
            if (shouldOpen)
            {
                UpdateAccountSelectionPanelWidth();
            }

            AccountSelectionPopup.IsOpen = shouldOpen;
            return;
        }

        if (MainWindow.Instance.mnv.SelectedItem is not FANavigationViewItem selectedItem)
        {
            return;
        }

        MainWindow.NavigatePage(
            new PageNavigateWay(typeof(AccountManagementPage), selectedItem),
            NavigateType.Append);
    }

    private void VersionSelectorButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (_isLaunching || _areVersionsLoading || _versionOptions.Count == 0)
        {
            return;
        }

        var shouldOpen = !VersionSelectionPopup.IsOpen;
        if (shouldOpen)
        {
            UpdateVersionSelectionPanelWidth();
        }

        VersionSelectionPopup.IsOpen = shouldOpen;
    }

    private void VersionSelectorButton_OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(VersionSelectorButton).Properties.PointerUpdateKind
            != PointerUpdateKind.RightButtonPressed)
        {
            return;
        }

        e.Handled = true;
        VersionSelectionPopup.IsOpen = false;
        NavigateToVersionManagementPage();
    }

    private void VersionSelectionPopup_OnOpened(object? sender, EventArgs e)
    {
        AnimateSelectionPanel(VersionSelectionPanel);
    }

    private void AccountSelectionPopup_OnOpened(object? sender, EventArgs e)
    {
        AnimateSelectionPanel(AccountSelectionPanel);
    }

    private void VersionSelectionListBox_OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_areVersionsLoading || _isUpdatingVersionSelection ||
            VersionSelectionListBox.SelectedItem is not LaunchVersionOption selectedVersion)
        {
            return;
        }

        _selectedVersion = selectedVersion;
        ClearRunningGameState();
        try
        {
            _versionManager.SetSelectedLaunchVersionName(
                selectedVersion.Version.RootPath,
                selectedVersion.VersionName);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Saving selected launch version");
        }

        VersionSelectionPopup.IsOpen = false;
        UpdateLaunchButtonState();
    }

    private void AccountSelectionListBox_OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_areAccountsLoading || AccountSelectionListBox.SelectedItem is not LaunchAccountOption selectedAccount)
        {
            return;
        }

        _selectedAccount = selectedAccount;
        Current.Config.SelectedLaunchAccountKey = selectedAccount.PersistentKey;
        try
        {
            ConfigManager.Save("app", Current.Config);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Saving selected launch account");
        }

        AccountSelectionPopup.IsOpen = false;
        UpdateAccountPresentation();
        UpdateLaunchButtonState();
    }

    private void ManageAccountsButton_OnClick(object? sender, RoutedEventArgs e)
    {
        AccountSelectionPopup.IsOpen = false;
        NavigateToAccountManagementPage();
    }

    async private void LaunchGameButton_Click(object? sender, RoutedEventArgs e)
    {
        if (_runningGame != null)
        {
            await TerminateRunningGameAsync();
            return;
        }

        if (_isLaunching || _isTerminatingGame)
        {
            return;
        }

        if (_versionOptions.Count == 0)
        {
            NavigateToDownloadPage();
            return;
        }

        if (_selectedVersion?.Version == null || _selectedAccount == null)
        {
            return;
        }

        var version = _selectedVersion.Version;
        var account = _selectedAccount.Account;
        _isLaunching = true;
        VersionSelectionPopup.IsOpen = false;
        AccountSelectionPopup.IsOpen = false;
        UpdateLaunchButtonState();
        using var cancellationSource = new CancellationTokenSource();
        var dialog = CreateLaunchDialog(out var progressText, out var progressRing);
        var isCancellationRequested = false;
        dialog.CloseButtonClick += (_, args) =>
        {
            if (!_isLaunching)
            {
                return;
            }

            args.Cancel = true;
            RequestLaunchCancellation();
        };
        dialog.Closing += (_, args) =>
        {
            if (!_isLaunching)
            {
                return;
            }

            args.Cancel = true;
            RequestLaunchCancellation();
        };
        _ = dialog.ShowAsync();

        string? resultMessage = null;
        var resultWasSuccessful = false;

        try
        {
            await Task.Yield();
            var progress = new Progress<GameLaunchProgress>(report =>
            {
                progressText.Text = I18nManager.Instance.GetString(
                    "Pages.LaunchPage.Status.LaunchingStep",
                    report.StepIndex,
                    report.TotalSteps,
                    GetStepDisplayText(report.Step));
            });

            var result = await Task.Run(
                () => _versionManager.LaunchGameAsync(
                    version,
                    Current.Config,
                    account,
                    shouldValidateAndCompleteMissingFiles: true,
                    progress: progress,
                    cancellationToken: cancellationSource.Token),
                cancellationSource.Token);

            SetRunningGame(result);
            resultMessage = I18nManager.Instance.GetString(
                result.WindowDetected
                    ? "Pages.LaunchPage.Status.WindowDetected"
                    : "Pages.LaunchPage.Status.WindowNotDetected");
            resultWasSuccessful = true;
        }
        catch (OperationCanceledException) when (cancellationSource.IsCancellationRequested)
        {
            _logger.Info("Game launch canceled by user.");
            resultMessage = I18nManager.Instance.GetString("Pages.LaunchPage.Status.LaunchCanceled");
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Launching game from launch page");
            resultMessage = string.Join(
                Environment.NewLine + Environment.NewLine,
                I18nManager.Instance.GetString("Pages.LaunchPage.Status.LaunchFailed", ex.Message),
                ex.StackTrace ?? ex.ToString());
        }
        finally
        {
            _isLaunching = false;
            UpdateLaunchButtonState();
        }

        if (resultMessage == null)
        {
            return;
        }

        if (resultWasSuccessful)
        {
            dialog.Hide();
            await MessageQueueHelper.ShowSuccess(
                I18nManager.Instance.GetString("Pages.LaunchPage.LaunchDialog.Title"),
                resultMessage);
            return;
        }

        progressRing.IsActive = false;
        progressRing.IsVisible = false;
        progressText.TextAlignment = TextAlignment.Left;
        progressText.Text = resultMessage;
        dialog.CloseButtonText = I18nManager.Instance.GetString("Pages.LaunchPage.LaunchDialog.Confirm");

        void RequestLaunchCancellation()
        {
            if (isCancellationRequested)
            {
                return;
            }

            isCancellationRequested = true;
            dialog.CloseButtonText = string.Empty;
            progressText.Text = I18nManager.Instance.GetString("Pages.LaunchPage.Status.Canceling");
            cancellationSource.Cancel();
        }
    }

    private void NavigateToDownloadPage()
    {
        if (MainWindow.Instance.mnv.SelectedItem is not FANavigationViewItem selectedItem)
        {
            return;
        }

        MainWindow.NavigatePage(
            new PageNavigateWay(typeof(DownloadMinecraftPageContent), selectedItem),
            NavigateType.New);
    }

    private void NavigateToAccountManagementPage()
    {
        if (MainWindow.Instance.mnv.SelectedItem is not FANavigationViewItem selectedItem)
        {
            return;
        }

        MainWindow.NavigatePage(
            new PageNavigateWay(typeof(AccountManagementPage), selectedItem),
            NavigateType.New);
    }

    private void NavigateToVersionManagementPage()
    {
        if (MainWindow.Instance.mnv.SelectedItem is not FANavigationViewItem selectedItem)
        {
            return;
        }

        MainWindow.NavigatePage(
            new PageNavigateWay(typeof(VersionManagementPage), selectedItem),
            NavigateType.New);
    }

    async private Task TerminateRunningGameAsync()
    {
        var runningGame = _runningGame;
        if (runningGame == null || _isTerminatingGame)
        {
            return;
        }

        _isTerminatingGame = true;
        UpdateLaunchButtonState();

        try
        {
            await Task.Run(() => _versionManager.TerminateGame(runningGame));
            await MessageQueueHelper.ShowSuccess(
                I18nManager.Instance.GetString("Messages.LaunchPage.GameClosed.Title"),
                I18nManager.Instance.GetString("Messages.LaunchPage.GameClosed.Content"));
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Terminating game from launch page");
        }
        finally
        {
            _isTerminatingGame = false;
            UpdateLaunchButtonState();
        }
    }

    private void SetRunningGame(GameLaunchResult result)
    {
        ClearRunningGameState();

        var process = result.Process;
        if (process == null)
        {
            UpdateLaunchButtonState();
            return;
        }

        try
        {
            if (process.HasExited)
            {
                UpdateLaunchButtonState();
                return;
            }

            _runningGame = result;
            process.Exited += RunningGameProcess_OnExited;
            process.EnableRaisingEvents = true;
        }
        catch (Exception ex)
        {
            _logger.Warn($"Unable to monitor game process {process.Id}: {ex.Message}");
            _runningGame = result;
        }

        UpdateLaunchButtonState();
    }

    private void RunningGameProcess_OnExited(object? sender, EventArgs e)
    {
        PostToUi(() =>
        {
            if (_runningGame?.Process != sender)
            {
                return;
            }

            ClearRunningGameState();
            UpdateLaunchButtonState();
        });
    }

    private void ClearRunningGameState()
    {
        if (_runningGame?.Process is { } process)
        {
            process.Exited -= RunningGameProcess_OnExited;
        }

        _runningGame = null;
    }

    private void UpdateAccountPresentation()
    {
        if (_areAccountsLoading)
        {
            UpdateAccountAvatar(null);
            SetAccountName(I18nManager.Instance.GetString("Pages.LaunchPage.LoadingButton"));
            AccountTypeText.Text = string.Empty;
            AnimateAutoWidth(AccountButton);
            return;
        }

        if (_selectedAccount == null)
        {
            UpdateAccountAvatar(null);
            SetAccountName(I18nManager.Instance.GetString("Pages.LaunchPage.NoAccount"));
            AccountTypeText.Text = string.Empty;
            AnimateAutoWidth(AccountButton);
            return;
        }

        SetAccountName(_selectedAccount.Account.Name);
        AccountTypeText.Text = _selectedAccount.AccountTypeText;
        UpdateAccountAvatar(_selectedAccount.Avatar);
        AnimateAutoWidth(AccountButton);
    }

    private void UpdateLaunchButtonState()
    {
        var hasVersions = _versionOptions.Count > 0;
        var hasRunningGame = _runningGame != null;
        var canLaunch = hasRunningGame
            ? !_isTerminatingGame
            : !_isLaunching &&
              !_isTerminatingGame &&
              !_areVersionsLoading &&
              (!hasVersions || (!_areAccountsLoading &&
                                _selectedVersion != null &&
                                _selectedAccount != null));

        LaunchGameButton.IsEnabled = canLaunch;
        LaunchGameButton.Classes.Set("accent", hasVersions && !hasRunningGame);
        LaunchGameButton.Classes.Set("launch-stop", hasRunningGame);
        LaunchStopIcon.IsVisible = hasRunningGame;
        VersionSelectorButton.IsEnabled = !_areVersionsLoading && !_isLaunching && hasVersions;
        AccountButton.IsEnabled = !_areAccountsLoading && !_isLaunching;
        LaunchButtonText.Text = hasRunningGame
            ? I18nManager.Instance.GetString("Pages.LaunchPage.TerminateGame")
            : _areVersionsLoading
            ? I18nManager.Instance.GetString("Pages.LaunchPage.LoadingButton")
            : hasVersions
            ? _selectedVersion?.VersionName ?? I18nManager.Instance.GetString("Pages.LaunchPage.SelectVersion")
            : I18nManager.Instance.GetString("Pages.LaunchPage.DownloadGame");
        AnimateAutoWidth(LaunchGameButton);
    }

    private static FAContentDialog CreateLaunchDialog(
        out TextBlock progressText,
        out FAProgressRing progressRing)
    {
        progressText = new TextBlock
        {
            MaxWidth = 360,
            Text = I18nManager.Instance.GetString("Pages.LaunchPage.Status.Starting"),
            TextAlignment = TextAlignment.Center,
            TextWrapping = TextWrapping.Wrap
        };

        progressRing = new FAProgressRing
        {
            Width = 36,
            Height = 36,
            IsActive = true,
            IsIndeterminate = true,
            HorizontalAlignment = HorizontalAlignment.Center
        };

        return new FAContentDialog
        {
            Title = I18nManager.Instance.GetString("Pages.LaunchPage.LaunchDialog.Title"),
            CloseButtonText = I18nManager.Instance.GetString("Pages.LaunchPage.LaunchDialog.Cancel"),
            Content = new Grid
            {
                MinWidth = 360,
                MinHeight = 180,
                Children =
                {
                    new StackPanel
                    {
                        HorizontalAlignment = HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center,
                        Orientation = Orientation.Vertical,
                        Spacing = 12,
                        Children =
                        {
                            progressRing,
                            new ScrollViewer
                            {
                                MaxHeight = 300,
                                Content = progressText
                            }
                        }
                    }
                }
            }
        };
    }

    private static string GetStepDisplayText(GameLaunchProgressStep step)
    {
        return I18nManager.Instance.GetString(step switch
        {
            GameLaunchProgressStep.PreLaunchCheck => "Pages.LaunchPage.Steps.PreLaunchCheck",
            GameLaunchProgressStep.DownloadMissingFiles => "Pages.LaunchPage.Steps.DownloadMissingFiles",
            GameLaunchProgressStep.ProcessAccount => "Pages.LaunchPage.Steps.ProcessAccount",
            GameLaunchProgressStep.ProcessLaunchArguments => "Pages.LaunchPage.Steps.ProcessLaunchArguments",
            GameLaunchProgressStep.ExtractLocalLibraries => "Pages.LaunchPage.Steps.ExtractLocalLibraries",
            GameLaunchProgressStep.StartGame => "Pages.LaunchPage.Steps.StartGame",
            GameLaunchProgressStep.WaitForGameWindow => "Pages.LaunchPage.Steps.WaitForGameWindow",
            _ => step.ToString()
        });
    }

    private static LaunchVersionOption CreateVersionOption(LocalGameVersionEntry version)
    {
        var displayType = version.VersionInfo == null
            ? GameVersionDisplayType.Release
            : GameVersionTypeClassifier.ClassifyLocalVersion(
                version.VersionInfo,
                version.VersionName,
                version.ClientVersionId);

        return new LaunchVersionOption(version, s_versionIcons[displayType]);
    }

    private static IImage CreateVersionIcon(string resourcePath)
    {
        using var stream = AssetLoader.Open(new Uri($"avares://LMCUI{resourcePath}"));
        return new Bitmap(stream);
    }

    private static IImage? LoadAccountAvatar(Account account)
    {
        if (string.IsNullOrWhiteSpace(account.AvatarBase64))
        {
            return null;
        }

        if (s_accountAvatarImages.TryGetValue(account.AvatarBase64, out var cachedAvatar))
        {
            return cachedAvatar;
        }

        var avatar = new Base64ToImageConverter().Convert(
            account.AvatarBase64,
            typeof(IImage),
            null,
            CultureInfo.InvariantCulture) as IImage;
        if (avatar != null)
        {
            s_accountAvatarImages.TryAdd(account.AvatarBase64, avatar);
        }

        return avatar;
    }

    private static LaunchAccountOption CreateAccountOption(Account account, IImage? avatar)
    {
        var accountTypeText = I18nManager.Instance.GetString($"Enums.AccountType.{account.Type}");
        return new LaunchAccountOption(
            account,
            avatar,
            accountTypeText,
            $"{(int)account.Type}:{account.Uuid}");
    }

    private void AnimateSelectionPanel(Border panel)
    {
        panel.Opacity = 0;
        if (panel.RenderTransform is TranslateTransform transform)
        {
            transform.Y = 12;
        }

        Dispatcher.UIThread.Post(() =>
        {
            panel.Opacity = 1;
            if (panel.RenderTransform is TranslateTransform translateTransform)
            {
                translateTransform.Y = 0;
            }
        }, DispatcherPriority.Render);
    }

    private static void AnimateAutoWidth(Control control)
    {
        if (!control.IsAttachedToVisualTree() || control.Bounds.Width <= 0)
        {
            return;
        }

        var currentWidth = control.Bounds.Width;
        var transitions = control.Transitions;
        control.Transitions = null;
        control.ClearValue(Layoutable.WidthProperty);
        control.Measure(Size.Infinity);
        var targetWidth = control.DesiredSize.Width;
        control.Width = currentWidth;
        control.Transitions = transitions;

        if (Math.Abs(targetWidth - currentWidth) < 0.5)
        {
            return;
        }

        var widthTransitions = control.Transitions ??= new Transitions();
        if (widthTransitions.OfType<DoubleTransition>().All(transition => transition.Property != Layoutable.WidthProperty))
        {
            widthTransitions.Add(new DoubleTransition
            {
                Property = Layoutable.WidthProperty,
                Duration = TimeSpan.FromMilliseconds(160),
                Easing = new CubicEaseOut()
            });
        }

        Dispatcher.UIThread.Post(() =>
        {
            if (control.IsAttachedToVisualTree())
            {
                control.Width = targetWidth;
            }
        }, DispatcherPriority.Render);
    }

    private void UpdateAccountAvatar(IImage? avatar)
    {
        var iconSource = avatar == null ? null : new FAImageIconSource { Source = avatar };
        if (!AccountAvatarHost.IsAttachedToVisualTree())
        {
            AccountAvatarIcon.IconSource = iconSource;
            return;
        }

        var requestId = ++_accountAvatarAnimationRequestId;
        AccountAvatarHost.Opacity = 0;
        DispatcherTimer.RunOnce(() =>
        {
            if (requestId != _accountAvatarAnimationRequestId || !AccountAvatarHost.IsAttachedToVisualTree())
            {
                return;
            }

            AccountAvatarIcon.IconSource = iconSource;
            AccountAvatarHost.Opacity = 1;
        }, TimeSpan.FromMilliseconds(100));
    }

    private void UpdateVersionSelectionPanelWidth()
    {
        var contentWidth = _versionOptions
            .Select(option => Math.Max(
                MeasureTextWidth(option.VersionName, AccountIdText),
                MeasureTextWidth(option.VersionSummary, AccountTypeText)))
            .DefaultIfEmpty(0)
            .Max();
        var versionWidth = contentWidth + VersionSelectionPanelHorizontalChrome;
        var loadingWidth = _isVersionCatalogLoading
            ? MeasureTextWidth(VersionSelectionLoadingText.Text ?? string.Empty, VersionSelectionLoadingText) + 56
            : 0;
        VersionSelectionPanel.Width = Math.Ceiling(Math.Max(versionWidth, loadingWidth));
    }

    private void UpdateAccountSelectionPanelWidth()
    {
        var accountContentWidth = _accountOptions
            .Select(option => Math.Max(
                MeasureTextWidth(option.Account.Name, AccountIdText),
                MeasureTextWidth(option.AccountTypeText, AccountTypeText)))
            .DefaultIfEmpty(0)
            .Max();
        var manageAccountsWidth = MeasureTextWidth(
                                      I18nManager.Instance.GetString("Pages.LaunchPage.AccountSelection.ManageAccounts"),
                                      AccountIdText)
                                  + 48;
        var contentWidth = Math.Max(accountContentWidth, manageAccountsWidth);
        AccountSelectionPanel.Width = Math.Ceiling(contentWidth + AccountSelectionPanelHorizontalChrome);
    }

    private static double MeasureTextWidth(string text, TextBlock styleSource)
    {
        var formattedText = new FormattedText(
            text,
            CultureInfo.CurrentCulture,
            styleSource.FlowDirection,
            new Typeface(
                styleSource.FontFamily,
                styleSource.FontStyle,
                styleSource.FontWeight,
                styleSource.FontStretch),
            styleSource.FontSize,
            null);
        return formattedText.WidthIncludingTrailingWhitespace;
    }

    private void SetAccountName(string accountName)
    {
        AccountIdText.Text = accountName;
        AccountIdText.TextTrimming = IsShortAccountName(accountName)
            ? TextTrimming.None
            : TextTrimming.CharacterEllipsis;
    }

    private static bool IsShortAccountName(string value)
    {
        return value.Length <= 16 && value.All(character =>
            char.IsAsciiLetterOrDigit(character) || character == '_');
    }

    private static void PostToUi(Action action)
    {
        if (Dispatcher.UIThread.CheckAccess())
        {
            action();
            return;
        }

        Dispatcher.UIThread.Post(action);
    }
}

internal sealed record LaunchVersionOption(LocalGameVersionEntry Version, IImage Icon)
{
    public string VersionName => Version.VersionName;

    public string VersionSummary => VersionSummaryPresentation.Build(
        Version,
        I18nManager.Instance.GetString("Pages.VersionManagePage.VersionType.UnknownClientVersion"));
}


internal sealed record LaunchAccountLoadItem(Account Account, IImage? Avatar);

internal sealed record LaunchAccountOption(
    Account Account,
    IImage? Avatar,
    string AccountTypeText,
    string PersistentKey);
