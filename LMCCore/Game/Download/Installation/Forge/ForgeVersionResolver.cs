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

using LMCCore.Game.Download.Model;
using LMCCore.Game.Model.Loaders;
using LMCCore.Utils;
using System.Text.Json;
using LMC.Basic.Logging;

namespace LMCCore.Game.Download.Installation.Forge;

internal static class ForgeVersionResolver
{
    private static readonly Logger s_logger = new("ForgeVersionResolver");
    public const string BranchMetadataKey = "branch";
    public const string InstallerFormatMetadataKey = "installerFormat";

    internal static async Task ResolveModpackMetadataAsync(
        string minecraftVersion, ModLoader loader, CancellationToken cancellationToken)
    {
        try
        {
            var url = $"https://bmclapi2.bangbang93.com/forge/minecraft/{Uri.EscapeDataString(minecraftVersion)}";
            using var response = await HttpUtils.CreateRequest(url).WithTimeout(TimeSpan.FromSeconds(15))
                .GetAsync(cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            using var catalog = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken)
                .ConfigureAwait(false);
            foreach (var item in catalog.RootElement.EnumerateArray())
            {
                if (item.GetProperty("version").GetString() != loader.VersionId)
                    continue;
                var installer = item.GetProperty("files").EnumerateArray().FirstOrDefault(file =>
                    file.GetProperty("category").GetString() == "installer"
                    && file.GetProperty("format").GetString() is "jar" or "zip");
                if (installer.ValueKind == JsonValueKind.Undefined)
                    break;
                loader.Metadata = new Dictionary<string, string?>
                {
                    [BranchMetadataKey] = item.TryGetProperty("branch", out var branch) ? branch.GetString() : null,
                    [InstallerFormatMetadataKey] = installer.GetProperty("format").GetString()
                };
                return;
            }
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException or InvalidOperationException or KeyNotFoundException
                                   or IOException or TaskCanceledException)
        {
            cancellationToken.ThrowIfCancellationRequested();
            s_logger.Warn($"Forge catalog unavailable; using artifact metadata: {ex.Message}");
        }

        var classifier = ResolveArtifactVersion(minecraftVersion, loader.VersionId);
        var prefix = $"{minecraftVersion}-{loader.VersionId}-";
        loader.Metadata = new Dictionary<string, string?>
        {
            [BranchMetadataKey] = classifier.StartsWith(prefix, StringComparison.Ordinal) ? classifier[prefix.Length..] : null,
            [InstallerFormatMetadataKey] = "jar"
        };
    }

    public static string ResolveArtifactVersion(string minecraftVersion, string forgeVersion)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(minecraftVersion);
        ArgumentException.ThrowIfNullOrWhiteSpace(forgeVersion);

        var parts = minecraftVersion.Split('.', StringSplitOptions.RemoveEmptyEntries);
        var minor = parts.Length > 1 && int.TryParse(parts[1], out var parsedMinor)
            ? parsedMinor
            : -1;
        var build = parts.Length > 2 && int.TryParse(parts[2], out var parsedBuild)
            ? parsedBuild
            : (int?)null;

        return minor switch
        {
            8 when build is null or 8 => $"{minecraftVersion}-{forgeVersion}",
            7 or 8 => $"{minecraftVersion}-{forgeVersion}-{minecraftVersion}",
            _ => $"{minecraftVersion}-{forgeVersion}"
        };

    }

    public static string ResolveInstallerDownloadUrl(string minecraftVersion, ModLoader loader)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(minecraftVersion);
        ArgumentNullException.ThrowIfNull(loader);
        ArgumentException.ThrowIfNullOrWhiteSpace(loader.VersionId);

        var classifier = ResolveInstallerClassifier(
            minecraftVersion,
            loader.VersionId,
            TryGetMetadataValue(loader, BranchMetadataKey));
        var format = ResolveInstallerFormat(loader);

        return $"{OfficialDownloadSource.ForgeMavenBaseUrl}/net/minecraftforge/forge/{classifier}/forge-{classifier}-installer.{format}";
    }

    private static string ResolveInstallerClassifier(
        string minecraftVersion,
        string forgeVersion,
        string? branch)
    {
        return string.IsNullOrWhiteSpace(branch)
            ? $"{minecraftVersion}-{forgeVersion}"
            : $"{minecraftVersion}-{forgeVersion}-{branch}";
    }

    private static string ResolveInstallerFormat(ModLoader loader)
    {
        var format = TryGetMetadataValue(loader, InstallerFormatMetadataKey);
        return string.IsNullOrWhiteSpace(format) ? "jar" : format;
    }

    private static string? TryGetMetadataValue(ModLoader loader, string key)
    {
        return loader.Metadata != null && loader.Metadata.TryGetValue(key, out var value)
            ? value
            : null;
    }
}
