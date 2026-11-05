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
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using LMCUI.I18n;
using LMC.Basic.Logging;
using LMCCore.Game.Mods;

namespace LMCUI.Pages.DownloadMinecraftPage.DownloadMinecraft;

public partial class LoaderSelectionStep : DownloadMinecraftStep
{
    private Action<(bool hasPrev, bool hasNext)>? _buttonStateChanged;
    private DownloadMinecraftWizardContext? _context;
    private DownloadMinecraftSelectionContext? _selection;
    private CancellationTokenSource? _loadCts;
    private bool _isFinal;
    private bool _isLoading;
    private CancellationTokenSource? _apiLoadCts;
    private IReadOnlyList<FabricApiVersion>? _apiVersions;

    public LoaderSelectionStep()
    {
        InitializeComponent();
    }

    public override void Enter(object? data, Action<(bool hasPrev, bool hasNext)> buttonStateChanged)
    {
        _buttonStateChanged = buttonStateChanged;
        _context = data as DownloadMinecraftWizardContext
            ?? throw new InvalidOperationException("Download wizard context is required.");

        StartCatalogLoad();
    }

    public override void Leave()
    {
        _apiLoadCts?.Cancel();
        _apiLoadCts = null;
        _loadCts?.Cancel();
        _loadCts = null;
        _context = null;
        _buttonStateChanged = null;
    }

    private void StartCatalogLoad()
    {
        if (_context == null)
            return;

        _loadCts?.Cancel();
        _loadCts = new CancellationTokenSource();
        _isLoading = true;
        ApplyLoadingState();
        _ = LoadCatalogAsync(_context, _loadCts);
    }

    private void RetryButton_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => StartCatalogLoad();

    public override (Type? type, object? data) NextStep()
    {
        return _selection == null
            ? (null, null)
            : (typeof(VersionNameStep), new DownloadMinecraftSelectionContext(
                _selection.SelectedRootPath,
                _selection.ManifestVersionId,
                _selection.DisplayType,
                _selection.FabricVersion,
                _selection.ForgeVersion,
                _selection.OptiFineVersion,
                _selection.FabricApi));
    }

    public override (Type? type, object? data) PreviousStep() => (null, null);

    public override bool IsFinalStep() => _isFinal;

    private void ComboBox_OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_isLoading)
            return;

        Validate();
    }

    private bool Validate()
    {
        var state = DownloadMinecraftWizardSupport.BuildLoaderSelectionState(
            _context,
            FabricComboBox.SelectedItem as string,
            ForgeComboBox.SelectedItem as ForgeVersionCatalogEntry,
            OptiFineComboBox.SelectedItem as string,
            _isLoading,
            NoneText,
            I18nManager.Instance.GetString("Pages.DownloadMinecraftPage.Wizard.Steps.LoaderSelectionStep.Validation.FabricOptiFineWarning"));

        _selection = state.Selection is { FabricVersion: not null } selection
            ? selection with { FabricApi = FabricApiComboBox.SelectedItem as FabricApiVersion }
            : state.Selection;
        UpdateFabricApiState(_selection?.FabricVersion != null);
        _isFinal = state.IsFinal;
        ValidationText.Text = state.ValidationMessage;
        ValidationText.IsVisible = state.IsValidationVisible;
        FabricComboBox.IsEnabled = state.IsFabricEnabled;
        ForgeComboBox.IsEnabled = state.IsForgeEnabled;
        OptiFineComboBox.IsEnabled = state.IsOptiFineEnabled;
        FabricExpander.IsEnabled = !_isLoading;
        ForgeExpander.IsEnabled = !_isLoading;
        OptiFineExpander.IsEnabled = !_isLoading;
        _buttonStateChanged?.Invoke((false, state.CanContinue));
        return state.CanContinue;
    }

    private void ApplyLoadingState()
    {
        LoadingStatus.IsVisible = true;
        LoadingRing.IsActive = true;
        LoadWarning.IsOpen = false;
        RetryButton.IsVisible = false;

        FabricComboBox.ItemsSource = new ObservableCollection<string> { NoneText };
        ForgeComboBox.ItemsSource = new ObservableCollection<string> { NoneText };
        OptiFineComboBox.ItemsSource = new ObservableCollection<string> { NoneText };

        FabricComboBox.SelectedIndex = 0;
        ForgeComboBox.SelectedIndex = 0;
        OptiFineComboBox.SelectedIndex = 0;
        Validate();
    }

    private void ApplyLoadedState()
    {
        FabricExpander.IsEnabled = true;
        ForgeExpander.IsEnabled = true;
        OptiFineExpander.IsEnabled = true;
        FabricComboBox.IsEnabled = true;
        ForgeComboBox.IsEnabled = true;
        OptiFineComboBox.IsEnabled = true;
    }

    private static string NoneText => I18nManager.Instance.GetString("Pages.DownloadMinecraftPage.Wizard.Steps.LoaderSelectionStep.None");

    private void UpdateFabricApiState(bool fabricSelected)
    {
        FabricApiExpander.IsVisible = fabricSelected;
        if (!fabricSelected)
        {
            if (FabricApiComboBox.SelectedIndex > 0)
                FabricApiComboBox.SelectedIndex = 0;
            return;
        }

        if (_apiVersions == null && _apiLoadCts == null && !FabricApiRetryButton.IsVisible)
            _ = LoadFabricApiAsync();
    }

    private void FabricApiRetryButton_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        _ = LoadFabricApiAsync();
    }

    private async Task LoadFabricApiAsync()
    {
        if (_context == null || _apiLoadCts != null)
            return;

        var context = _context;
        using var request = new CancellationTokenSource();
        _apiLoadCts = request;
        FabricApiRing.IsVisible = FabricApiRing.IsActive = true;
        FabricApiRetryButton.IsVisible = false;
        FabricApiComboBox.IsEnabled = false;
        FabricApiComboBox.ItemsSource = new object[] { NoneText };
        FabricApiComboBox.SelectedIndex = 0;
        FabricApiExpander.Description = I18nManager.Instance.GetString(
            "Pages.DownloadMinecraftPage.Wizard.Steps.LoaderSelectionStep.FabricApi.Loading");
        try
        {
            var versions = await FabricApiCatalog.GetVersionsAsync(context.ManifestVersionId, request.Token);
            if (request.IsCancellationRequested || !ReferenceEquals(_context, context))
                return;

            _apiVersions = versions;
            FabricApiComboBox.ItemsSource = versions.Cast<object>().Prepend(NoneText).ToArray();
            FabricApiComboBox.SelectedIndex = 0;
            FabricApiExpander.Description = I18nManager.Instance.GetString(
                "Pages.DownloadMinecraftPage.Wizard.Steps.LoaderSelectionStep.FabricApi."
                + (versions.Count == 0 ? "Empty" : "Description"));
        }
        catch (OperationCanceledException) when (request.IsCancellationRequested) { }
        catch (Exception ex)
        {
            new Logger("FabricApiCatalog").Error(ex, "Loading Fabric API versions");
            if (!request.IsCancellationRequested && ReferenceEquals(_context, context))
            {
                FabricApiExpander.Description = I18nManager.Instance.GetString(
                    "Pages.DownloadMinecraftPage.Wizard.Steps.LoaderSelectionStep.FabricApi.Failed");
                FabricApiRetryButton.IsVisible = true;
            }
        }
        finally
        {
            if (ReferenceEquals(_apiLoadCts, request))
            {
                _apiLoadCts = null;
                FabricApiRing.IsVisible = FabricApiRing.IsActive = false;
                FabricApiComboBox.IsEnabled = _apiVersions is { Count: > 0 };
            }
        }
    }

    async private Task LoadCatalogAsync(DownloadMinecraftWizardContext context, CancellationTokenSource requestCts)
    {
        try
        {
            var loadResult = await DownloadMinecraftWizardSupport.LoadCatalogAsync(context.ManifestVersionId, requestCts.Token).ConfigureAwait(true);
            if (requestCts.IsCancellationRequested || !ReferenceEquals(_loadCts, requestCts) || !ReferenceEquals(_context, context))
                return;

            _isLoading = false;
            LoadingRing.IsActive = false;
            LoadingStatus.IsVisible = false;
            LoadWarning.IsOpen = loadResult.Exception != null;
            RetryButton.IsVisible = loadResult.Exception != null;
            ApplyCatalog(loadResult.Catalog);
            ApplyLoadedState();
            Validate();
        }
        finally
        {
            if (ReferenceEquals(_loadCts, requestCts))
                _loadCts = null;
            requestCts.Dispose();
        }
    }

    private void ApplyCatalog(DownloadMinecraftVersionCatalogResult catalog)
    {
        FabricComboBox.ItemsSource = new ObservableCollection<string>(catalog.FabricVersions.Prepend(NoneText));
        ForgeComboBox.ItemsSource = new ObservableCollection<object>(catalog.ForgeVersions.Cast<object>().Prepend(NoneText));
        OptiFineComboBox.ItemsSource = new ObservableCollection<string>(catalog.OptiFineVersions.Prepend(NoneText));

        FabricComboBox.SelectedIndex = 0;
        ForgeComboBox.SelectedIndex = 0;
        OptiFineComboBox.SelectedIndex = 0;
    }
}

public sealed class DownloadMinecraftVersionCatalogResult
{
    public List<string> FabricVersions { get; } = [];
    public List<ForgeVersionCatalogEntry> ForgeVersions { get; } = [];
    public List<string> OptiFineVersions { get; } = [];
}
