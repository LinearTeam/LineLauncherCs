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
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using FluentAvalonia.UI.Controls;
using LMC.Basic.Logging;
using LMCCore.Game.Model;
using LMCCore.Game.Mods.Local;
using LMCUI.I18n;
using LMCUI.Utils;

namespace LMCUI.Pages.VersionManagePage.Mods;

public partial class VersionModsPage : PageBase, IVersionContextPage
{
    private readonly Logger _logger = new("VersionModsPage");
    private LocalModManager? _manager;
    private IReadOnlyList<LocalModFile> _mods = Array.Empty<LocalModFile>();
    private readonly ObservableCollection<ModListItem> _visibleMods = [];
    private CancellationTokenSource? _scanCts;
    private bool _isBusy;
    private bool _updatingList;
    private bool _isPageLoaded;
    private bool _pendingExternalRefresh;
    public LocalGameVersionEntry? TargetVersion { get; private set; }

    public VersionModsPage() : base("Pages.VersionModsPage.Title", "VersionModsPage")
    {
        InitializeComponent();
        ModList.ItemsSource = _visibleMods;
        Loaded += (_, _) =>
        {
            _isPageLoaded = true;
            LocalModManager.Changed += OnModsChanged;
            _ = RefreshAsync();
        };
        Unloaded += (_, _) =>
        {
            _isPageLoaded = false;
            _scanCts?.Cancel();
            LocalModManager.Changed -= OnModsChanged;
        };
    }

    public override void ProcessParameter(object? param)
    {
        base.ProcessParameter(param);
        if (param is not LocalGameVersionEntry version)
            return;
        TargetVersion = version;
        _manager = new LocalModManager(version.VersionDirectory);
        VersionNameText.Text = version.VersionName;
        Title = Text("Title");
    }

    private static string Text(string key, params object[] args) =>
        I18nManager.Instance.GetString("Pages.VersionModsPage." + key, args);

    private async Task RefreshAsync(bool clearError = true, bool showLoading = true)
    {
        if (_manager == null)
            return;
        if (_isBusy)
        {
            if (!showLoading)
                _pendingExternalRefresh = true;
            return;
        }
        _scanCts?.Cancel();
        using var request = new CancellationTokenSource();
        _scanCts = request;
        SetBusy(true);
        LoadingPanel.IsVisible = LoadingRing.IsActive = showLoading;
        if (showLoading)
            ModList.IsVisible = EmptyPanel.IsVisible = false;
        if (clearError)
            ErrorBar.IsOpen = false;
        try
        {
            _mods = await _manager.ScanAsync(request.Token);
        }
        catch (OperationCanceledException) when (request.IsCancellationRequested) { }
        catch (Exception ex)
        {
            ShowError(ex);
        }
        finally
        {
            if (ReferenceEquals(_scanCts, request))
            {
                _scanCts = null;
                LoadingPanel.IsVisible = LoadingRing.IsActive = false;
                SetBusy(false);
                ApplyFilter();
            }
        }
    }

    private void ApplyFilter()
    {
        if (ModList == null || StateFilter == null || SearchBox == null || LoadingPanel.IsVisible)
            return;
        var query = SearchBox.Text?.Trim() ?? string.Empty;
        var selectedPaths = GetSelectedMods().Select(mod => mod.FilePath).ToHashSet(StringComparer.Ordinal);
        var visible = _mods.Where(mod =>
                (StateFilter.SelectedIndex != 1 || mod.IsEnabled)
                && (StateFilter.SelectedIndex != 2 || !mod.IsEnabled)
                && (mod.Name.Contains(query, StringComparison.OrdinalIgnoreCase)
                    || mod.FileName.Contains(query, StringComparison.OrdinalIgnoreCase)))
            .ToArray();
        _updatingList = true;
        try
        {
            var paths = visible.Select(mod => mod.FilePath).ToHashSet(StringComparer.Ordinal);
            for (var i = _visibleMods.Count - 1; i >= 0; i--)
                if (!paths.Contains(_visibleMods[i].File.FilePath))
                    _visibleMods.RemoveAt(i);

            var existing = _visibleMods.ToDictionary(item => item.File.FilePath, StringComparer.Ordinal);
            for (var i = 0; i < visible.Length; i++)
            {
                var file = visible[i];
                if (existing.TryGetValue(file.FilePath, out var item))
                {
                    UpdateModItem(item, file);
                    var current = _visibleMods.IndexOf(item);
                    if (current != i)
                        _visibleMods.Move(current, i);
                }
                else
                {
                    item = new ModListItem(file, string.Empty);
                    UpdateModItem(item, file);
                    _visibleMods.Insert(i, item);
                }

                if (selectedPaths.Contains(file.FilePath) && !ModList.SelectedItems!.Contains(item))
                    ModList.SelectedItems.Add(item);
            }
        }
        finally
        {
            _updatingList = false;
        }
        UpdateSelectionCommands();
        ModList.IsVisible = visible.Length > 0;
        EmptyPanel.IsVisible = visible.Length == 0;
        EmptyTitleText.Text = Text(_mods.Count == 0 ? "EmptyTitle" : "NoResults");
        EmptyDescriptionText.Text = Text(_mods.Count == 0 ? "EmptyDescription" : "NoResultsDescription");
        EmptyImportButton.IsVisible = _mods.Count == 0;
    }

    public Task RefreshAfterExternalImportAsync() => RefreshAsync(clearError: false, showLoading: false);

    private void OnModsChanged(string versionDirectory)
    {
        if (!string.Equals(TargetVersion?.VersionDirectory, versionDirectory,
                OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))
            return;
        Avalonia.Threading.Dispatcher.UIThread.Post(async () =>
        {
            if (!_isPageLoaded || !string.Equals(TargetVersion?.VersionDirectory, versionDirectory,
                    OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))
                return;
            await RefreshAfterExternalImportAsync();
        });
    }

    private static void UpdateModItem(ModListItem item, LocalModFile file)
    {
        item.Update(file, Text(file.IsEnabled ? "DisableMod" : "EnableMod"));
    }

    private void SetBusy(bool busy)
    {
        _isBusy = busy;
        SelectionCommands.IsEnabled = !busy;
        ImportButton.IsEnabled = RefreshButton.IsEnabled = ModList.IsEnabled = EmptyImportButton.IsEnabled = !busy;
        if (!busy && _pendingExternalRefresh && _isPageLoaded)
        {
            _pendingExternalRefresh = false;
            Avalonia.Threading.Dispatcher.UIThread.Post(async () =>
            {
                if (_isPageLoaded)
                    await RefreshAfterExternalImportAsync();
            });
        }
    }

    private void ShowError(Exception ex)
    {
        _logger.Error(ex, "Managing version mods");
        ErrorBar.Message = Text("Failed", ex.Message);
        ErrorBar.IsOpen = true;
    }

    private async Task RunMutationAsync(Func<LocalModManager, Task> action, bool rescan = true)
    {
        if (_manager == null || _isBusy)
            return;
        SetBusy(true);
        ErrorBar.IsOpen = false;
        try
        {
            await action(_manager);
            if (rescan)
                _mods = await _manager.ScanAsync();
        }
        catch (Exception ex)
        {
            ShowError(ex);
            try
            {
                _mods = await _manager.ScanAsync();
            }
            catch (Exception scanError)
            {
                _logger.Error(scanError, "Refreshing mods after a failed operation");
            }
        }
        finally
        {
            SetBusy(false);
            ApplyFilter();
        }
    }

    private async void ImportButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (_manager == null || _isBusy)
            return;
        try
        {
            var files = await CrossPlatformUtils.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                AllowMultiple = true,
                Title = Text("Import"),
                FileTypeFilter = [new FilePickerFileType(Text("JarFiles")) { Patterns = ["*.jar"] }]
            });
            var paths = files.Select(file => file.TryGetLocalPath()).OfType<string>().ToArray();
            if (paths.Length > 0)
                await ImportPathsAsync(paths);
        }
        catch (Exception ex)
        {
            ShowError(ex);
        }
    }

    private async void ModToggle_OnIsCheckedChanged(object? sender, RoutedEventArgs e)
    {
        if (_updatingList || _isBusy || sender is not ToggleSwitch { DataContext: ModListItem item } toggle
            || (toggle.IsChecked == true) == item.IsEnabled)
            return;
        await SetModsEnabledAsync([item.File], toggle.IsChecked == true);
    }

    private async void OpenFolderButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (_manager == null)
            return;
        try
        {
            await Task.Run(() => Directory.CreateDirectory(_manager.ModsDirectory));
            CrossPlatformUtils.OpenFolderInExplorer(_manager.ModsDirectory);
        }
        catch (Exception ex)
        {
            ShowError(ex);
        }
    }

    private async void RefreshButton_OnClick(object? sender, RoutedEventArgs e) => await RefreshAsync();
    private void SearchBox_OnTextChanged(object? sender, TextChangedEventArgs e) => ApplyFilter();
    private void StateFilter_OnSelectionChanged(object? sender, SelectionChangedEventArgs e) => ApplyFilter();

    private void Page_OnSizeChanged(object? sender, SizeChangedEventArgs e)
    {
        PageLayout.Margin = e.NewSize.Width < 700
            ? new Thickness(20, 20, 20, 30) : new Thickness(60, 20, 40, 30);
    }

    public async Task ImportPathsAsync(string[] paths)
    {
        if (_manager == null || _isBusy || paths.Length == 0)
            return;
        SetBusy(true);
        try
        {
            var manager = _manager;
            var versionName = TargetVersion?.VersionName ?? VersionNameText.Text ?? string.Empty;
            await ModImportDialogs.ImportAndNotifyAsync(manager, versionName, paths);
            _mods = await _manager.ScanAsync();
            ApplyFilter();
        }
        catch (Exception ex)
        {
            ShowError(ex);
        }
        finally
        {
            SetBusy(false);
            ApplyFilter();
        }
    }
}
