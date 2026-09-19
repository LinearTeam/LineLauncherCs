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

using LMCCore.Game.Model;
using LMCCore.Game.Model.LocalVersion;

namespace LMCCore.Java;

internal static class JavaCompatibilityPolicy
{
    public static JavaRequirement Resolve(LocalGameVersionEntry version)
    {
        ArgumentNullException.ThrowIfNull(version);

        var versionInfo = version.VersionInfo;
        var clientVersion = ParseMinecraftVersion(version.ClientVersionId) ??
                            ParseMinecraftVersion(versionInfo?.ClientVersion) ??
                            ParseMinecraftVersion(versionInfo?.Id);
        var declaredMajor = versionInfo?.RequiredJavaVersion?.MajorVersion;
        var preferredMajor = declaredMajor is > 0
            ? declaredMajor.Value
            : ResolvePreferredMajor(clientVersion);
        var minimumMajor = declaredMajor is > 0 ? declaredMajor.Value : preferredMajor;
        int? maximumMajor = null;

        if (RequiresJava7(version, clientVersion))
        {
            return new JavaRequirement(7, 7, 7);
        }

        if (RequiresJava8OrEarlier(version, clientVersion))
        {
            preferredMajor = Math.Min(preferredMajor, 8);
            minimumMajor = Math.Min(minimumMajor, 8);
            maximumMajor = 8;
        }

        return new JavaRequirement(preferredMajor, minimumMajor, maximumMajor);
    }

    public static bool IsCompatible(LocalJava java, JavaRequirement requirement)
    {
        return java.Version.Major >= requirement.MinimumMajor &&
               (requirement.MaximumMajor == null || java.Version.Major <= requirement.MaximumMajor);
    }

    private static int ResolvePreferredMajor(MinecraftVersion? version)
    {
        if (version == null)
        {
            return 21;
        }

        if (version.Value.IsAtLeast(1, 20, 5))
        {
            return 21;
        }

        if (version.Value.IsAtLeast(1, 18))
        {
            return 17;
        }

        if (version.Value.IsAtLeast(1, 17))
        {
            return 16;
        }

        return 8;
    }

    private static bool RequiresJava7(LocalGameVersionEntry version, MinecraftVersion? clientVersion)
    {
        return clientVersion is { } parsedVersion && parsedVersion.IsAtMost(1, 7, 2) &&
               version.ModLoaders.Any(loader => loader.Type == LocalGameModLoaderType.Forge);
    }

    private static bool RequiresJava8OrEarlier(LocalGameVersionEntry version, MinecraftVersion? clientVersion)
    {
        if (clientVersion is not { } parsedVersion || !parsedVersion.IsAtMost(1, 12, 2))
        {
            return false;
        }

        if (version.ModLoaders.Any(loader => loader.Type == LocalGameModLoaderType.OptiFine))
        {
            return true;
        }

        return string.Equals(
                   version.VersionInfo?.MainClass,
                   "net.minecraft.launchwrapper.Launch",
                   StringComparison.Ordinal) ||
               version.VersionInfo?.Libraries.Any(library =>
                   library.Name.StartsWith("net.minecraft:launchwrapper:", StringComparison.OrdinalIgnoreCase)) == true;
    }

    private static MinecraftVersion? ParseMinecraftVersion(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var numericPart = value.Split('-', 2)[0];
        var parts = numericPart.Split('.');
        if (parts.Length < 2 || !int.TryParse(parts[0], out var major) || !int.TryParse(parts[1], out var minor))
        {
            return null;
        }

        var patch = parts.Length > 2 && int.TryParse(parts[2], out var parsedPatch) ? parsedPatch : 0;
        return new MinecraftVersion(major, minor, patch);
    }

    private readonly record struct MinecraftVersion(int Major, int Minor, int Patch)
    {
        public bool IsAtLeast(int major, int minor, int patch = 0)
        {
            return CompareTo(major, minor, patch) >= 0;
        }

        public bool IsAtMost(int major, int minor, int patch = 0)
        {
            return CompareTo(major, minor, patch) <= 0;
        }

        private int CompareTo(int major, int minor, int patch)
        {
            var majorComparison = Major.CompareTo(major);
            if (majorComparison != 0)
            {
                return majorComparison;
            }

            var minorComparison = Minor.CompareTo(minor);
            return minorComparison != 0 ? minorComparison : Patch.CompareTo(patch);
        }
    }
}

internal readonly record struct JavaRequirement(int PreferredMajor, int MinimumMajor, int? MaximumMajor);
