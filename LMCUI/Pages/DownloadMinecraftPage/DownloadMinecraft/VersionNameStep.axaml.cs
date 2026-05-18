using System;
using System.IO;
using Avalonia.Controls;
using LMCUI.I18n;

namespace LMCUI.Pages.DownloadMinecraftPage;

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
            GetLoaderSummary());

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

        return (typeof(LoaderSelectionStep), new DownloadMinecraftWizardContext(_context.SelectedRootPath, _context.ManifestVersionId));
    }

    public override bool IsFinalStep() => _isFinal;

    public override DownloadableVersionSelection? GetResult() => _result;

    private void VersionNameBox_OnTextChanged(object? sender, TextChangedEventArgs e)
    {
        Validate();
    }

    private bool Validate()
    {
        if (_context == null)
            return false;

        var versionName = VersionNameBox.Text ?? string.Empty;
        string? error = null;

        if (string.IsNullOrWhiteSpace(versionName))
        {
            error = I18nManager.Instance.GetString("Pages.DownloadMinecraftPage.Wizard.Steps.VersionNameStep.Validation.Empty");
        }
        else if (!IsLegalPathSegment(versionName))
        {
            error = I18nManager.Instance.GetString("Pages.DownloadMinecraftPage.Wizard.Steps.VersionNameStep.Validation.InvalidPath");
        }
        else
        {
            var versionsDirectory = Path.Combine(_context.SelectedRootPath, "versions");
            var targetDirectory = Path.Combine(versionsDirectory, versionName);
            if (Directory.Exists(targetDirectory))
            {
                error = I18nManager.Instance.GetString("Pages.DownloadMinecraftPage.Wizard.Steps.VersionNameStep.Validation.AlreadyExists");
            }
        }

        if (error != null)
        {
            ValidationText.Text = error;
            ValidationText.IsVisible = true;
            _result = null;
            _isFinal = false;
            _buttonStateChanged?.Invoke((true, false));
            return false;
        }

        ValidationText.IsVisible = false;
        _result = new DownloadableVersionSelection(
            _context.ManifestVersionId,
            versionName,
            _context.SelectedRootPath,
            _context.FabricVersion,
            _context.ForgeVersion,
            _context.OptiFineVersion);
        _isFinal = true;
        _buttonStateChanged?.Invoke((true, true));
        return true;
    }

    private static bool IsLegalPathSegment(string versionName)
    {
        if (versionName == "." || versionName == "..")
            return false;

        if (versionName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 ||
            versionName.Contains(Path.DirectorySeparatorChar) ||
            versionName.Contains(Path.AltDirectorySeparatorChar))
        {
            return false;
        }

        if (OperatingSystem.IsWindows())
        {
            if (versionName.EndsWith(' ') || versionName.EndsWith('.'))
                return false;

            var deviceName = Path.GetFileNameWithoutExtension(versionName);
            if (deviceName.Equals("CON", StringComparison.OrdinalIgnoreCase) ||
                deviceName.Equals("PRN", StringComparison.OrdinalIgnoreCase) ||
                deviceName.Equals("AUX", StringComparison.OrdinalIgnoreCase) ||
                deviceName.Equals("NUL", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (deviceName.Length == 4 &&
                (deviceName.StartsWith("COM", StringComparison.OrdinalIgnoreCase) ||
                 deviceName.StartsWith("LPT", StringComparison.OrdinalIgnoreCase)) &&
                char.IsAsciiDigit(deviceName[3]) &&
                deviceName[3] != '0')
            {
                return false;
            }
        }

        return true;
    }

    private string GetLoaderSummary()
    {
        if (_context == null)
            return string.Empty;

        return $"{_context.FabricVersion ?? "-"} | {_context.ForgeVersion ?? "-"} | {_context.OptiFineVersion ?? "-"}";
    }
}
