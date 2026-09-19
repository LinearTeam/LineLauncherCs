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

using System.IO.Compression;
using LMCCore.Game.Download.Installation.Forge;

namespace LMCCore.Game.Download.Installation.OptiFine;

internal static class OptiFineInstallerArchiveEditor
{
    public static void CopyInstallerWithoutModsToml(string sourceJarPath, string destinationJarPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceJarPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationJarPath);

        Directory.CreateDirectory(Path.GetDirectoryName(destinationJarPath)!);

        using var sourceStream = File.OpenRead(sourceJarPath);
        using var sourceArchive = new ZipArchive(sourceStream, ZipArchiveMode.Read);
        using var destinationStream = File.Create(destinationJarPath);
        using var destinationArchive = new ZipArchive(destinationStream, ZipArchiveMode.Create);

        foreach (var entry in sourceArchive.Entries)
        {
            if (entry.FullName.Equals("META-INF/mods.toml", StringComparison.OrdinalIgnoreCase) ||
                entry.FullName.Equals("/META-INF/mods.toml", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var targetEntry = destinationArchive.CreateEntry(entry.FullName, CompressionLevel.Optimal);
            using var sourceEntryStream = entry.Open();
            using var targetEntryStream = targetEntry.Open();
            sourceEntryStream.CopyTo(targetEntryStream);
        }
    }

    public static bool HasPatcher(string jarPath)
    {
        return ForgeInstallerArchiveReader.ContainsEntry(jarPath, "optifine/Patcher.class");
    }

    public static string? ResolveEmbeddedLaunchWrapperVersion(string jarPath)
    {
        var version = ForgeInstallerArchiveReader.ReadOptionalTextEntry(jarPath, "launchwrapper-of.txt");
        return string.IsNullOrWhiteSpace(version) ? null : version.Trim();
    }

    public static bool HasEmbeddedLaunchWrapperJar(string jarPath)
    {
        return ForgeInstallerArchiveReader.ContainsEntry(jarPath, "launchwrapper-2.0.jar");
    }

    public static bool HasEmbeddedLaunchWrapperOfJar(string jarPath, string version)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(version);
        return ForgeInstallerArchiveReader.ContainsEntry(jarPath, $"launchwrapper-of-{version}.jar");
    }

    public static void ExtractEmbeddedLibrary(string jarPath, string entryName, string destinationPath)
    {
        ForgeInstallerArchiveReader.ExtractEntry(jarPath, entryName, destinationPath);
    }
}
