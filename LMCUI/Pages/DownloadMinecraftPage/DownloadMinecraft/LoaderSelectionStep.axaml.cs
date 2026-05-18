using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Avalonia.Controls;
using LMCCore.Utils;
using LMCUI.I18n;

namespace LMCUI.Pages.DownloadMinecraftPage;

public partial class LoaderSelectionStep : DownloadMinecraftStep
{
    private Action<(bool hasPrev, bool hasNext)>? _buttonStateChanged;
    private DownloadMinecraftWizardContext? _context;
    private DownloadMinecraftSelectionContext? _selection;
    private bool _isFinal;
    private bool _isLoading;
    private bool _fabricChosen;
    private bool _forgeChosen;
    private bool _optiFineChosen;

    public LoaderSelectionStep()
    {
        InitializeComponent();
    }

    public override async void Enter(object? data, Action<(bool hasPrev, bool hasNext)> buttonStateChanged)
    {
        _buttonStateChanged = buttonStateChanged;
        _context = data as DownloadMinecraftWizardContext
            ?? throw new InvalidOperationException("Download wizard context is required.");

        _isLoading = true;
        ApplyLoadingState();
        var catalog = await DownloadMinecraftVersionCatalog.LoadAsync(_context.ManifestVersionId).ConfigureAwait(true);
        _isLoading = false;

        FabricComboBox.ItemsSource = new ObservableCollection<string>(catalog.FabricVersions.Prepend(NoneText));
        ForgeComboBox.ItemsSource = new ObservableCollection<string>(catalog.ForgeVersions.Prepend(NoneText));
        OptiFineComboBox.ItemsSource = new ObservableCollection<string>(catalog.OptiFineVersions.Prepend(NoneText));

        FabricComboBox.SelectedIndex = 0;
        ForgeComboBox.SelectedIndex = 0;
        OptiFineComboBox.SelectedIndex = 0;

        ApplyLoadedState();
        Validate();
    }

    public override (Type? type, object? data) NextStep()
    {
        return _selection == null
            ? (null, null)
            : (typeof(VersionNameStep), new DownloadMinecraftSelectionContext(
                _selection.SelectedRootPath,
                _selection.ManifestVersionId,
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

        _fabricChosen = !IsNone(FabricComboBox.SelectedItem as string);
        _forgeChosen = !IsNone(ForgeComboBox.SelectedItem as string);
        _optiFineChosen = !IsNone(OptiFineComboBox.SelectedItem as string);

        ApplyMutualExclusion();
        Validate();
    }

    private bool Validate()
    {
        if (_context == null || _isLoading)
        {
            _buttonStateChanged?.Invoke((false, false));
            return false;
        }

        var warning = _fabricChosen && _optiFineChosen
            ? I18nManager.Instance.GetString("Pages.DownloadMinecraftPage.Wizard.Steps.LoaderSelectionStep.Validation.FabricOptiFineWarning")
            : string.Empty;

        ValidationText.Text = warning;
        ValidationText.IsVisible = !string.IsNullOrWhiteSpace(warning);

        _selection = new DownloadMinecraftSelectionContext(
            _context.SelectedRootPath,
            _context.ManifestVersionId,
            _fabricChosen ? FabricComboBox.SelectedItem as string : null,
            _forgeChosen ? ForgeComboBox.SelectedItem as string : null,
            _optiFineChosen ? OptiFineComboBox.SelectedItem as string : null);

        _isFinal = false;
        _buttonStateChanged?.Invoke((false, !_isLoading));
        return true;
    }

    private void ApplyLoadingState()
    {
        _selection = null;
        _isFinal = false;
        FabricExpander.IsEnabled = false;
        ForgeExpander.IsEnabled = false;
        OptiFineExpander.IsEnabled = false;
        FabricComboBox.IsEnabled = false;
        ForgeComboBox.IsEnabled = false;
        OptiFineComboBox.IsEnabled = false;

        FabricComboBox.ItemsSource = new ObservableCollection<string> { LoadingText };
        ForgeComboBox.ItemsSource = new ObservableCollection<string> { LoadingText };
        OptiFineComboBox.ItemsSource = new ObservableCollection<string> { LoadingText };

        FabricComboBox.SelectedIndex = 0;
        ForgeComboBox.SelectedIndex = 0;
        OptiFineComboBox.SelectedIndex = 0;
        ValidationText.IsVisible = false;
        ValidationText.Text = string.Empty;
        _buttonStateChanged?.Invoke((false, false));
    }

    private void ApplyLoadedState()
    {
        FabricExpander.IsEnabled = true;
        ForgeExpander.IsEnabled = true;
        OptiFineExpander.IsEnabled = true;
        FabricComboBox.IsEnabled = true;
        ForgeComboBox.IsEnabled = true;
        OptiFineComboBox.IsEnabled = true;
        ApplyMutualExclusion();
    }

    private void ApplyMutualExclusion()
    {
        if (_isLoading)
            return;

        FabricComboBox.IsEnabled = !_forgeChosen;
        ForgeComboBox.IsEnabled = !_fabricChosen;
        OptiFineComboBox.IsEnabled = true;

        FabricExpander.IsEnabled = true;
        ForgeExpander.IsEnabled = true;
        OptiFineExpander.IsEnabled = true;
    }

    private static string NoneText => I18nManager.Instance.GetString("Pages.DownloadMinecraftPage.Wizard.Steps.LoaderSelectionStep.None");
    private static string LoadingText => I18nManager.Instance.GetString("Pages.DownloadMinecraftPage.Wizard.Steps.LoaderSelectionStep.Loading");

    private static bool IsNone(string? value)
    {
        return string.IsNullOrWhiteSpace(value) || string.Equals(value, NoneText, StringComparison.Ordinal);
    }
}

public static class DownloadMinecraftVersionCatalog
{
    public static async Task<DownloadMinecraftVersionCatalogResult> LoadAsync(string mcVersion)
    {
        var result = new DownloadMinecraftVersionCatalogResult();

        using var fabricResponse = await HttpUtils.CreateRequest("https://meta.fabricmc.net/v2/versions").GetAsync();
        fabricResponse.EnsureSuccessStatusCode();
        using var fabricDoc = JsonDocument.Parse(await fabricResponse.Content.ReadAsStringAsync());
        var fabricRoot = fabricDoc.RootElement;
        if (fabricRoot.TryGetProperty("game", out var fabricGames) &&
            fabricGames.EnumerateArray().Any(item => string.Equals(item.GetProperty("version").GetString(), mcVersion, StringComparison.OrdinalIgnoreCase)))
        {
            foreach (var item in fabricRoot.GetProperty("loader").EnumerateArray())
            {
                var version = item.GetProperty("version").GetString();
                if (!string.IsNullOrWhiteSpace(version))
                {
                    result.FabricVersions.Add(version);
                }
            }
        }

        using var forgeResponse = await HttpUtils.CreateRequest($"https://bmclapi2.bangbang93.com/forge/minecraft/{mcVersion}").GetAsync();
        if (forgeResponse.IsSuccessStatusCode)
        {
            using var forgeDoc = JsonDocument.Parse(await forgeResponse.Content.ReadAsStringAsync());
            foreach (var item in forgeDoc.RootElement.EnumerateArray())
            {
                var version = item.GetProperty("version").GetString();
                if (!string.IsNullOrWhiteSpace(version))
                {
                    result.ForgeVersions.Add(version);
                }
            }

            result.ForgeVersions.Sort((a, b) => string.CompareOrdinal(b, a));
        }

        using var optiResponse = await HttpUtils.CreateRequest($"https://bmclapi2.bangbang93.com/optifine/{mcVersion}").GetAsync();
        if (optiResponse.IsSuccessStatusCode)
        {
            var text = await optiResponse.Content.ReadAsStringAsync();
            if (!string.IsNullOrWhiteSpace(text) && !string.Equals(text.Trim(), "[]", StringComparison.Ordinal))
            {
                using var optiDoc = JsonDocument.Parse(text);
                foreach (var item in optiDoc.RootElement.EnumerateArray())
                {
                    var patch = item.GetProperty("patch").GetString();
                    if (!string.IsNullOrWhiteSpace(patch))
                    {
                        result.OptiFineVersions.Insert(0, patch);
                    }
                }
            }
        }

        return result;
    }
}

public sealed class DownloadMinecraftVersionCatalogResult
{
    public List<string> FabricVersions { get; } = [];
    public List<string> ForgeVersions { get; } = [];
    public List<string> OptiFineVersions { get; } = [];
}
