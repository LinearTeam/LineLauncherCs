using System;
using System.IO;
using System.Text.Json;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using LMCCore.Utils;

namespace LMCUI.Pages.DownloadMinecraftPage.DownloadMinecraft;

internal interface IDownloadMinecraftCatalogClient
{
    Task<string> GetFabricVersionsJsonAsync(CancellationToken cancellationToken);
    Task<string?> GetForgeVersionsJsonAsync(string mcVersion, CancellationToken cancellationToken);
    Task<string?> GetOptiFineVersionsJsonAsync(string mcVersion, CancellationToken cancellationToken);
}

internal sealed class HttpDownloadMinecraftCatalogClient : IDownloadMinecraftCatalogClient
{
    public async Task<string> GetFabricVersionsJsonAsync(CancellationToken cancellationToken)
    {
        using var response = await HttpUtils.CreateRequest("https://meta.fabricmc.net/v2/versions").GetAsync(cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync(cancellationToken);
    }

    public async Task<string?> GetForgeVersionsJsonAsync(string mcVersion, CancellationToken cancellationToken)
    {
        using var response = await HttpUtils.CreateRequest($"https://bmclapi2.bangbang93.com/forge/minecraft/{mcVersion}")
            .GetAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        return await response.Content.ReadAsStringAsync(cancellationToken);
    }

    public async Task<string?> GetOptiFineVersionsJsonAsync(string mcVersion, CancellationToken cancellationToken)
    {
        using var response = await HttpUtils.CreateRequest($"https://bmclapi2.bangbang93.com/optifine/{mcVersion}")
            .GetAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        return await response.Content.ReadAsStringAsync(cancellationToken);
    }
}

internal sealed record DownloadMinecraftCatalogLoadResult(
    DownloadMinecraftVersionCatalogResult Catalog,
    Exception? Exception);

internal sealed record LoaderSelectionUiState(
    DownloadMinecraftSelectionContext? Selection,
    string ValidationMessage,
    bool IsValidationVisible,
    bool IsFinal,
    bool CanContinue,
    bool IsFabricEnabled,
    bool IsForgeEnabled,
    bool IsOptiFineEnabled);

internal sealed record VersionNameValidationResult(
    DownloadableVersionSelection? Selection,
    string? ErrorMessage,
    bool IsFinal)
{
    public bool IsValid => Selection != null;
}

internal static class DownloadMinecraftWizardSupport
{
    private const string OptiFineDisplayPrefix = "HD_U_";

    public static async Task<DownloadMinecraftCatalogLoadResult> LoadCatalogAsync(
        string mcVersion,
        CancellationToken cancellationToken,
        IDownloadMinecraftCatalogClient? client = null)
    {
        var catalog = new DownloadMinecraftVersionCatalogResult();
        client ??= new HttpDownloadMinecraftCatalogClient();

        try
        {
            using var fabricDoc = JsonDocument.Parse(await client.GetFabricVersionsJsonAsync(cancellationToken));
            var fabricRoot = fabricDoc.RootElement;
            if (fabricRoot.TryGetProperty("game", out var fabricGames) &&
                fabricGames.EnumerateArray().Any(item =>
                    string.Equals(item.GetProperty("version").GetString(), mcVersion, StringComparison.OrdinalIgnoreCase)))
            {
                foreach (var item in fabricRoot.GetProperty("loader").EnumerateArray())
                {
                    var version = item.GetProperty("version").GetString();
                    if (!string.IsNullOrWhiteSpace(version))
                    {
                        catalog.FabricVersions.Add(version);
                    }
                }
            }

            var forgeJson = await client.GetForgeVersionsJsonAsync(mcVersion, cancellationToken);
            if (!string.IsNullOrWhiteSpace(forgeJson) && !string.Equals(forgeJson.Trim(), "[]", StringComparison.Ordinal))
            {
                using var forgeDoc = JsonDocument.Parse(forgeJson);
                foreach (var item in forgeDoc.RootElement.EnumerateArray())
                {
                    var version = item.GetProperty("version").GetString();
                    if (!string.IsNullOrWhiteSpace(version))
                    {
                        catalog.ForgeVersions.Add(version);
                    }
                }

                catalog.ForgeVersions.Sort((a, b) => string.CompareOrdinal(b, a));
            }

            var optiFineJson = await client.GetOptiFineVersionsJsonAsync(mcVersion, cancellationToken);
            if (!string.IsNullOrWhiteSpace(optiFineJson) && !string.Equals(optiFineJson.Trim(), "[]", StringComparison.Ordinal))
            {
                using var optiFineDoc = JsonDocument.Parse(optiFineJson);
                foreach (var item in optiFineDoc.RootElement.EnumerateArray())
                {
                    var patch = item.GetProperty("patch").GetString();
                    if (!string.IsNullOrWhiteSpace(patch))
                    {
                        var displayPatch = FormatOptiFineVersionForDisplay(patch);
                        if (!string.IsNullOrWhiteSpace(displayPatch))
                        {
                            catalog.OptiFineVersions.Insert(0, displayPatch);
                        }
                    }
                }
            }

            return new DownloadMinecraftCatalogLoadResult(catalog, null);
        }
        catch (Exception ex)
        {
            return new DownloadMinecraftCatalogLoadResult(catalog, ex);
        }
    }

    public static LoaderSelectionUiState BuildLoaderSelectionState(
        DownloadMinecraftWizardContext? context,
        string? selectedFabric,
        string? selectedForge,
        string? selectedOptiFine,
        bool isLoading,
        string noneText,
        string conflictWarningText)
    {
        if (context == null || isLoading)
        {
            return new LoaderSelectionUiState(null, string.Empty, false, false, false, false, false, false);
        }

        var fabricChosen = !IsNone(selectedFabric, noneText);
        var forgeChosen = !IsNone(selectedForge, noneText);
        var optiFineChosen = !IsNone(selectedOptiFine, noneText);
        var validationMessage = fabricChosen && optiFineChosen ? conflictWarningText : string.Empty;

        return new LoaderSelectionUiState(
            new DownloadMinecraftSelectionContext(
                context.SelectedRootPath,
                context.ManifestVersionId,
                fabricChosen ? selectedFabric : null,
                forgeChosen ? selectedForge : null,
                optiFineChosen ? NormalizeOptiFineVersionFromDisplay(selectedOptiFine) : null),
            validationMessage,
            !string.IsNullOrWhiteSpace(validationMessage),
            false,
            true,
            !forgeChosen,
            !fabricChosen,
            true);
    }

    public static VersionNameValidationResult ValidateVersionName(
        DownloadMinecraftSelectionContext? context,
        string? versionName,
        Func<string, bool> directoryExists)
    {
        if (context == null)
        {
            return new VersionNameValidationResult(null, null, false);
        }

        var normalizedVersionName = versionName ?? string.Empty;
        string? errorMessage = null;

        if (string.IsNullOrWhiteSpace(normalizedVersionName))
        {
            errorMessage = "Pages.DownloadMinecraftPage.Wizard.Steps.VersionNameStep.Validation.Empty";
        }
        else if (!IsLegalPathSegment(normalizedVersionName))
        {
            errorMessage = "Pages.DownloadMinecraftPage.Wizard.Steps.VersionNameStep.Validation.InvalidPath";
        }
        else
        {
            var targetDirectory = Path.Combine(context.SelectedRootPath, "versions", normalizedVersionName);
            if (directoryExists(targetDirectory))
            {
                errorMessage = "Pages.DownloadMinecraftPage.Wizard.Steps.VersionNameStep.Validation.AlreadyExists";
            }
        }

        if (errorMessage != null)
        {
            return new VersionNameValidationResult(null, errorMessage, false);
        }

        return new VersionNameValidationResult(
            new DownloadableVersionSelection(
                context.ManifestVersionId,
                normalizedVersionName,
                context.SelectedRootPath,
                context.FabricVersion,
                context.ForgeVersion,
                context.OptiFineVersion),
            null,
            true);
    }

    public static string BuildLoaderSummary(DownloadMinecraftSelectionContext? context)
    {
        if (context == null)
        {
            return string.Empty;
        }

        return $"{context.FabricVersion ?? "-"} | {context.ForgeVersion ?? "-"} | {FormatOptiFineVersionForDisplay(context.OptiFineVersion) ?? "-"}";
    }

    public static string? FormatOptiFineVersionForDisplay(string? optiFineVersion)
    {
        if (string.IsNullOrWhiteSpace(optiFineVersion))
        {
            return optiFineVersion;
        }

        return optiFineVersion.StartsWith(OptiFineDisplayPrefix, StringComparison.OrdinalIgnoreCase)
            ? optiFineVersion
            : $"{OptiFineDisplayPrefix}{optiFineVersion}";
    }

    public static string? NormalizeOptiFineVersionFromDisplay(string? optiFineVersion)
    {
        if (string.IsNullOrWhiteSpace(optiFineVersion))
        {
            return optiFineVersion;
        }

        return optiFineVersion.StartsWith(OptiFineDisplayPrefix, StringComparison.OrdinalIgnoreCase)
            ? optiFineVersion[OptiFineDisplayPrefix.Length..]
            : optiFineVersion;
    }

    public static DownloadMinecraftWizardContext CreatePreviousContext(DownloadMinecraftSelectionContext context)
    {
        return new DownloadMinecraftWizardContext(context.SelectedRootPath, context.ManifestVersionId);
    }

    public static (bool hasPrev, bool hasNext, bool isFinal) BuildDialogButtonState(DownloadMinecraftStep step, (bool hasPrev, bool hasNext) state)
    {
        return (state.hasPrev, state.hasNext, step.IsFinalStep());
    }

    private static bool IsNone(string? value, string noneText)
    {
        return string.IsNullOrWhiteSpace(value) || string.Equals(value, noneText, StringComparison.Ordinal);
    }

    private static bool IsLegalPathSegment(string versionName)
    {
        if (versionName == "." || versionName == "..")
        {
            return false;
        }

        if (versionName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 ||
            versionName.Contains(Path.DirectorySeparatorChar) ||
            versionName.Contains(Path.AltDirectorySeparatorChar))
        {
            return false;
        }

        if (OperatingSystem.IsWindows())
        {
            if (versionName.EndsWith(' ') || versionName.EndsWith('.'))
            {
                return false;
            }

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
}
