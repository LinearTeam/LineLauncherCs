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

namespace LMCCore.Game.Download.Installation.Forge;

internal static class ForgeVersionResolver
{
    public const string BranchMetadataKey = "branch";
    public const string InstallerFormatMetadataKey = "installerFormat";

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
