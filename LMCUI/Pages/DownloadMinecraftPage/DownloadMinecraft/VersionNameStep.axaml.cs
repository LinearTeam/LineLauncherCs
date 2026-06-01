using System;
using System.IO;
using Avalonia.Controls;
using LMCUI.I18n;

namespace LMCUI.Pages.DownloadMinecraftPage.DownloadMinecraft;

public partial class VersionNameStep : DownloadMinecraftStep
{
    private Action<(bool hasPrev, bool hasNext)>? _buttonStateChanged;
    private DownloadMinecraftSelectionContext? _context;
    private DownloadableVersionSelection? _result;
    private bool _isFinal;

    public VersionNameStep()
    {
        InitializeComponent();
    }

    public override void Enter(object? data, Action<(bool hasPrev, bool hasNext)> buttonStateChanged)
    {
        _buttonStateChanged = buttonStateChanged;
        _context = data as DownloadMinecraftSelectionContext
            ?? throw new InvalidOperationException("Download wizard context is required.");

        SelectedVersionText.Text = I18nManager.Instance.GetString(
            "Pages.DownloadMinecraftPage.Wizard.Steps.VersionNameStep.SelectedVersion",
            _context.ManifestVersionId,
            _context.SelectedRootPath,
            DownloadMinecraftWizardSupport.BuildLoaderSummary(_context));

        if (string.IsNullOrWhiteSpace(VersionNameBox.Text))
        {
            VersionNameBox.Text = _context.ManifestVersionId;
        }

        Validate();
    }

    public override (Type? type, object? data) NextStep()
    {
        return Validate() ? (null, null) : (null, null);
    }

    public override (Type? type, object? data) PreviousStep()
    {
        if (_context == null)
            return (null, null);

        return (typeof(DownloadMinecraft.LoaderSelectionStep), DownloadMinecraftWizardSupport.CreatePreviousContext(_context));
    }

    public override bool IsFinalStep() => _isFinal;

    public override DownloadableVersionSelection? GetResult() => _result;

    private void VersionNameBox_OnTextChanged(object? sender, TextChangedEventArgs e)
    {
        Validate();
    }

    private bool Validate()
    {
        var validation = DownloadMinecraftWizardSupport.ValidateVersionName(
            _context,
            VersionNameBox.Text,
            Directory.Exists);

        if (!validation.IsValid)
        {
            ValidationText.Text = validation.ErrorMessage == null
                ? string.Empty
                : I18nManager.Instance.GetString(validation.ErrorMessage);
            ValidationText.IsVisible = true;
            _result = null;
            _isFinal = false;
            _buttonStateChanged?.Invoke((true, false));
            return false;
        }

        ValidationText.IsVisible = false;
        _result = validation.Selection;
        _isFinal = validation.IsFinal;
        _buttonStateChanged?.Invoke((true, true));
        return true;
    }
}
