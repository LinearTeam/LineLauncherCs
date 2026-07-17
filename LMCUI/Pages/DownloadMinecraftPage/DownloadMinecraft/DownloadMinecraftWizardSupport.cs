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
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using LMCCore.Game.Download;
using LMCCore.Game.Download.Model;
using LMCCore.Game.Model.Loaders;
using LMCCore.Game.Versioning.Discovery;
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
    private readonly DownloadSourceManager _downloadSourceManager = DownloadSourceManager.CreateDefault();

    public async Task<string> GetFabricVersionsJsonAsync(CancellationToken cancellationToken)
    {
        const string url = "https://meta.fabricmc.net/v2/versions";
        using var response = await HttpUtils.CreateRequest(_downloadSourceManager.TransformUrl(url) ?? url).GetAsync(cancellationToken);
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
        mcVersion = OptiFineCatalogVersionSupport.NormalizeRequestVersion(mcVersion);
        using var response = await HttpUtils.CreateRequest($"https://bmclapi2.bangbang93.com/optifine/{mcVersion}")
            .GetAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        return await response.Content.ReadAsStringAsync(cancellationToken);
    }
}

internal static class OptiFineCatalogVersionSupport
{
    public static string NormalizeRequestVersion(string mcVersion)
    {
        return mcVersion switch
        {
            "1.8" => "1.8.0",
            "1.9" => "1.9.0",
            _ => mcVersion
        };
    }

    public static string BuildDisplayIdentifier(string type, string patch)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(type);
        ArgumentException.ThrowIfNullOrWhiteSpace(patch);
        return $"{type}_{patch}";
    }

    public static bool TryParseSelectedVersion(string? value, out string type, out string patch)
    {
        type = string.Empty;
        patch = string.Empty;

        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var lastSeparator = value.LastIndexOf('_');
        if (lastSeparator > 0 && lastSeparator < value.Length - 1)
        {
            type = value[..lastSeparator];
            patch = value[(lastSeparator + 1)..];
            return true;
        }

        type = "HD_U";
        patch = value;
        return true;
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

public sealed record ForgeVersionCatalogEntry(
    string VersionId,
    string? Branch,
    string InstallerFormat)
{
    public override string ToString() => VersionId;
}

internal sealed record VersionNameValidationResult(
    DownloadableVersionSelection? Selection,
    string? ErrorMessage,
    bool IsFinal)
{
    public bool IsValid => Selection != null;
}

internal static class DownloadMinecraftWizardSupport
{
    private const string LegacyOptiFineDisplayPrefix = "HD_U_";
    private const string ForgeBranchMetadataKey = "branch";
    private const string ForgeInstallerFormatMetadataKey = "installerFormat";
    private readonly record struct MinecraftReleaseVersion(int Major, int Minor, int Patch)
        : IComparable<MinecraftReleaseVersion>
    {
        public int CompareTo(MinecraftReleaseVersion other)
        {
            var majorComparison = Major.CompareTo(other.Major);
            if (majorComparison != 0)
            {
                return majorComparison;
            }

            var minorComparison = Minor.CompareTo(other.Minor);
            if (minorComparison != 0)
            {
                return minorComparison;
            }

            return Patch.CompareTo(other.Patch);
        }
    }

    public async static Task<DownloadMinecraftCatalogLoadResult> LoadCatalogAsync(
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
                        catalog.ForgeVersions.Add(new ForgeVersionCatalogEntry(
                            version,
                            item.TryGetProperty("branch", out var branchProperty) ? branchProperty.GetString() : null,
                            ResolveForgeInstallerFormat(item)));
                    }
                }

                catalog.ForgeVersions.Sort((a, b) => string.CompareOrdinal(b.VersionId, a.VersionId));
            }

            var optiFineJson = await client.GetOptiFineVersionsJsonAsync(mcVersion, cancellationToken);
            if (!string.IsNullOrWhiteSpace(optiFineJson) && !string.Equals(optiFineJson.Trim(), "[]", StringComparison.Ordinal))
            {
                using var optiFineDoc = JsonDocument.Parse(optiFineJson);
                foreach (var item in optiFineDoc.RootElement.EnumerateArray())
                {
                    var type = item.TryGetProperty("type", out var typeProperty)
                        ? typeProperty.GetString()
                        : null;
                    var patch = item.GetProperty("patch").GetString();
                    if (!string.IsNullOrWhiteSpace(patch))
                    {
                        var displayPatch = !string.IsNullOrWhiteSpace(type)
                            ? OptiFineCatalogVersionSupport.BuildDisplayIdentifier(type, patch)
                            : FormatOptiFineVersionForDisplay(patch);
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
        ForgeVersionCatalogEntry? selectedForge,
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
        var forgeChosen = selectedForge != null;
        var optiFineChosen = !IsNone(selectedOptiFine, noneText);
        var fabricOptiFineCompatible = IsFabricOptiFineCombinationAllowed(context);
        var hasFabricOptiFineConflict = fabricChosen && optiFineChosen;
        var hasBlockingFabricOptiFineConflict = hasFabricOptiFineConflict && !fabricOptiFineCompatible;

        return new LoaderSelectionUiState(
            new DownloadMinecraftSelectionContext(
                context.SelectedRootPath,
                context.ManifestVersionId,
                context.DisplayType,
                fabricChosen ? selectedFabric : null,
                selectedForge,
                optiFineChosen ? NormalizeOptiFineVersionFromDisplay(selectedOptiFine) : null),
            hasBlockingFabricOptiFineConflict ? conflictWarningText : string.Empty,
            hasBlockingFabricOptiFineConflict,
            false,
            !hasBlockingFabricOptiFineConflict,
            !forgeChosen && !(optiFineChosen && !fabricOptiFineCompatible),
            !fabricChosen,
            !(fabricChosen && !fabricOptiFineCompatible));
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

    public static DownloadableGameVersion CreateDownloadRequest(DownloadableVersionSelection selection)
    {
        ArgumentNullException.ThrowIfNull(selection);

        var loaders = new List<ModLoader>();
        if (!string.IsNullOrWhiteSpace(selection.FabricVersion))
        {
            loaders.Add(new ModLoader
            {
                Type = ModLoaderType.Fabric,
                VersionId = selection.FabricVersion
            });
        }

        if (selection.ForgeVersion != null)
        {
            loaders.Add(new ModLoader
            {
                Type = ModLoaderType.Forge,
                VersionId = selection.ForgeVersion.VersionId,
                Metadata = CreateForgeLoaderMetadata(selection.ForgeVersion)
            });
        }

        return new DownloadableGameVersion
        {
            RootPath = selection.SelectedRootPath,
            VersionId = selection.ManifestVersionId,
            VersionName = selection.VersionName,
            Loaders = [..loaders],
            OptiFine = selection.OptiFineVersion
        };
    }

    public static string BuildLoaderSummary(DownloadMinecraftSelectionContext? context)
    {
        if (context == null)
        {
            return string.Empty;
        }

        return $"{context.FabricVersion ?? "-"} | {context.ForgeVersion?.VersionId ?? "-"} | {FormatOptiFineVersionForDisplay(context.OptiFineVersion) ?? "-"}";
    }

    public static string? FormatOptiFineVersionForDisplay(string? optiFineVersion)
    {
        if (string.IsNullOrWhiteSpace(optiFineVersion))
        {
            return optiFineVersion;
        }

        return OptiFineCatalogVersionSupport.TryParseSelectedVersion(optiFineVersion, out _, out _)
            ? optiFineVersion
            : $"{LegacyOptiFineDisplayPrefix}{optiFineVersion}";
    }

    public static string? NormalizeOptiFineVersionFromDisplay(string? optiFineVersion)
    {
        if (string.IsNullOrWhiteSpace(optiFineVersion))
        {
            return optiFineVersion;
        }

        return optiFineVersion;
    }

    public static DownloadMinecraftWizardContext CreatePreviousContext(DownloadMinecraftSelectionContext context)
    {
        return new DownloadMinecraftWizardContext(
            context.SelectedRootPath,
            context.ManifestVersionId,
            context.DisplayType);
    }

    public static (bool hasPrev, bool hasNext, bool isFinal) BuildDialogButtonState(DownloadMinecraftStep step, (bool hasPrev, bool hasNext) state)
    {
        return (state.hasPrev, state.hasNext, step.IsFinalStep());
    }

    public static bool ShouldCancelDialogClose(bool isDialogBusy, bool allowProgrammaticClose)
    {
        return isDialogBusy && !allowProgrammaticClose;
    }

    private static bool IsNone(string? value, string noneText)
    {
        return string.IsNullOrWhiteSpace(value) || string.Equals(value, noneText, StringComparison.Ordinal);
    }

    private static string ResolveForgeInstallerFormat(JsonElement item)
    {
        if (!item.TryGetProperty("files", out var files) || files.ValueKind != JsonValueKind.Array)
        {
            return "jar";
        }

        foreach (var file in files.EnumerateArray())
        {
            if (!file.TryGetProperty("category", out var categoryProperty) ||
                !string.Equals(categoryProperty.GetString(), "installer", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var format = file.TryGetProperty("format", out var formatProperty)
                ? formatProperty.GetString()
                : null;
            if (!string.IsNullOrWhiteSpace(format))
            {
                return format;
            }
        }

        return "jar";
    }

    private static Dictionary<string, string?> CreateForgeLoaderMetadata(ForgeVersionCatalogEntry forgeVersion)
    {
        return new Dictionary<string, string?>
        {
            [ForgeBranchMetadataKey] = forgeVersion.Branch,
            [ForgeInstallerFormatMetadataKey] = forgeVersion.InstallerFormat
        };
    }

    private static bool IsFabricOptiFineCombinationAllowed(DownloadMinecraftWizardContext context)
    {
        if (context.DisplayType is GameVersionDisplayType.Snapshot or GameVersionDisplayType.AprilFools or GameVersionDisplayType.Old)
        {
            return true;
        }

        if (!TryParseReleaseVersion(context.ManifestVersionId, out var version))
        {
            return true;
        }

        return version.CompareTo(new MinecraftReleaseVersion(1, 14, 0)) >= 0 &&
               version.CompareTo(new MinecraftReleaseVersion(1, 20, 4)) <= 0;
    }

    private static bool TryParseReleaseVersion(string? versionId, out MinecraftReleaseVersion version)
    {
        version = default;
        if (string.IsNullOrWhiteSpace(versionId))
        {
            return false;
        }

        var segments = versionId.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (segments.Length < 2 || segments.Length > 3)
        {
            return false;
        }

        if (!int.TryParse(segments[0], out var major) ||
            !int.TryParse(segments[1], out var minor))
        {
            return false;
        }

        var patch = 0;
        if (segments.Length == 3 && !int.TryParse(segments[2], out patch))
        {
            return false;
        }

        version = new MinecraftReleaseVersion(major, minor, patch);
        return true;
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
