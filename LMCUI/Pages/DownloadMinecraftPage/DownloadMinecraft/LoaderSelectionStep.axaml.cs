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

namespace LMCUI.Pages.DownloadMinecraftPage.DownloadMinecraft;

public partial class LoaderSelectionStep : DownloadMinecraftStep
{
    private Action<(bool hasPrev, bool hasNext)>? _buttonStateChanged;
    private DownloadMinecraftWizardContext? _context;
    private DownloadMinecraftSelectionContext? _selection;
    private CancellationTokenSource? _loadCts;
    private bool _isFinal;
    private bool _isLoading;

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
                _selection.OptiFineVersion));
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

        _selection = state.Selection;
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
