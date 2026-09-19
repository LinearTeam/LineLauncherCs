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

using LMCCore.Game.Model.LocalVersion;

namespace LMCCore.Game.Versioning.Discovery;

public static class LocalGameModLoaderResolver
{
    public static IReadOnlyList<LocalGameModLoader> Resolve(
        LocalVersionInfo versionInfo,
        string clientVersionId)
    {
        ArgumentNullException.ThrowIfNull(versionInfo);

        var loaders = new List<LocalGameModLoader>();
        foreach (var library in versionInfo.Libraries)
        {
            if (TryGetCoordinatePart(library.Name, "net.fabricmc:fabric-loader:", out var fabricVersion))
            {
                AddLoader(loaders, LocalGameModLoaderType.Fabric, fabricVersion);
                continue;
            }

            if (TryGetCoordinatePart(library.Name, "net.minecraftforge:forge:", out var forgeCoordinate))
            {
                var forgeVersion = ResolveForgeVersion(forgeCoordinate, clientVersionId, versionInfo.Id);
                if (!string.IsNullOrWhiteSpace(forgeVersion))
                {
                    AddLoader(loaders, LocalGameModLoaderType.Forge, forgeVersion);
                }

                continue;
            }

            if (TryGetCoordinatePart(library.Name, "optifine:OptiFine:", out var optiFineCoordinate))
            {
                var optiFineVersion = ResolveOptiFineVersion(optiFineCoordinate, clientVersionId, versionInfo.Id);
                if (!string.IsNullOrWhiteSpace(optiFineVersion))
                {
                    AddLoader(loaders, LocalGameModLoaderType.OptiFine, optiFineVersion);
                }
            }
        }

        return loaders.AsReadOnly();
    }

    private static bool TryGetCoordinatePart(string name, string prefix, out string coordinate)
    {
        coordinate = string.Empty;
        if (!name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        coordinate = name[prefix.Length..];
        var classifierSeparator = coordinate.IndexOf(':');
        if (classifierSeparator >= 0)
        {
            coordinate = coordinate[..classifierSeparator];
        }

        return !string.IsNullOrWhiteSpace(coordinate);
    }

    private static string? ResolveForgeVersion(
        string coordinate,
        string clientVersionId,
        string versionInfoId)
    {
        return ResolveForgeVersionForMinecraft(coordinate, clientVersionId)
               ?? ResolveForgeVersionForMinecraft(coordinate, versionInfoId)
               ?? GetVersionAfterSeparator(coordinate, '-');
    }

    private static string? ResolveForgeVersionForMinecraft(string coordinate, string? minecraftVersion)
    {
        var forgeVersion = RemoveMinecraftVersionPrefix(coordinate, minecraftVersion, '-');
        if (forgeVersion is null)
        {
            return null;
        }

        return RemoveMinecraftVersionSuffix(forgeVersion, minecraftVersion, '-') ?? forgeVersion;
    }

    private static string? ResolveOptiFineVersion(
        string coordinate,
        string clientVersionId,
        string versionInfoId)
    {
        return RemoveMinecraftVersionPrefix(coordinate, clientVersionId, '_')
               ?? RemoveMinecraftVersionPrefix(coordinate, versionInfoId, '_')
               ?? GetVersionAfterSeparator(coordinate, '_');
    }

    private static string? RemoveMinecraftVersionPrefix(
        string coordinate,
        string? minecraftVersion,
        char separator)
    {
        if (string.IsNullOrWhiteSpace(minecraftVersion))
        {
            return null;
        }

        var prefix = minecraftVersion + separator;
        return coordinate.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
            ? coordinate[prefix.Length..]
            : null;
    }

    private static string? RemoveMinecraftVersionSuffix(
        string coordinate,
        string? minecraftVersion,
        char separator)
    {
        if (string.IsNullOrWhiteSpace(minecraftVersion))
        {
            return null;
        }

        var suffix = separator + minecraftVersion;
        return coordinate.EndsWith(suffix, StringComparison.OrdinalIgnoreCase)
            ? coordinate[..^suffix.Length]
            : null;
    }

    private static string? GetVersionAfterSeparator(string coordinate, char separator)
    {
        var separatorIndex = coordinate.IndexOf(separator);
        return separatorIndex >= 0 && separatorIndex < coordinate.Length - 1
            ? coordinate[(separatorIndex + 1)..]
            : null;
    }

    private static void AddLoader(
        ICollection<LocalGameModLoader> loaders,
        LocalGameModLoaderType type,
        string version)
    {
        if (loaders.Any(loader => loader.Type == type &&
                                  string.Equals(loader.VersionId, version, StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        loaders.Add(new LocalGameModLoader(type, version));
    }
}
