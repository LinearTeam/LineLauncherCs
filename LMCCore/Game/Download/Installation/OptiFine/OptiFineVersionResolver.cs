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

namespace LMCCore.Game.Download.Installation.OptiFine;

internal static class OptiFineVersionResolver
{
    public const string FixedType = "HD_U";

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

        type = FixedType;
        patch = value;
        return true;
    }

    public static string NormalizeCatalogMinecraftVersion(string mcVersion)
    {
        return mcVersion switch
        {
            "1.8" => "1.8.0",
            "1.9" => "1.9.0",
            _ => mcVersion
        };
    }

    public static string BuildInstallerUrl(string mcVersion, string type, string patch)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(mcVersion);
        ArgumentException.ThrowIfNullOrWhiteSpace(type);
        ArgumentException.ThrowIfNullOrWhiteSpace(patch);
        return $"https://bmclapi2.bangbang93.com/optifine/{NormalizeCatalogMinecraftVersion(mcVersion)}/{type}/{patch}";
    }

    public static string BuildCoordinateVersion(string mcVersion, string type, string patch)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(mcVersion);
        ArgumentException.ThrowIfNullOrWhiteSpace(type);
        ArgumentException.ThrowIfNullOrWhiteSpace(patch);
        return $"{mcVersion}_{type}_{patch}";
    }

    public static string BuildLibraryCoordinate(string mcVersion, string type, string patch)
    {
        return $"optifine:OptiFine:{BuildCoordinateVersion(mcVersion, type, patch)}";
    }

    public static string BuildInstallerLibraryCoordinate(string mcVersion, string type, string patch)
    {
        return $"{BuildLibraryCoordinate(mcVersion, type, patch)}:installer";
    }
}
