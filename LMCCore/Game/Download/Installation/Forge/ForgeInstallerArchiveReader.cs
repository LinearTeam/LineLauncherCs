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
using System.Text;

namespace LMCCore.Game.Download.Installation.Forge;

internal static class ForgeInstallerArchiveReader
{
    public static bool ContainsEntry(string jarPath, string entryPath)
    {
        using var stream = File.OpenRead(jarPath);
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read);
        return archive.GetEntry(entryPath) != null;
    }

    public static bool ContainsEntryEndingWith(string jarPath, string entryFileName)
    {
        using var stream = File.OpenRead(jarPath);
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read);
        return archive.Entries.Any(entry =>
            entry.FullName.EndsWith(entryFileName, StringComparison.OrdinalIgnoreCase));
    }

    public static string ReadRequiredTextEntry(string jarPath, string entryPath)
    {
        return ReadOptionalTextEntry(jarPath, entryPath)
               ?? throw new FileNotFoundException($"Entry '{entryPath}' was not found in installer '{jarPath}'.", entryPath);
    }

    public static string? ReadOptionalTextEntry(string jarPath, string entryPath)
    {
        using var stream = File.OpenRead(jarPath);
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read);
        var entry = archive.GetEntry(entryPath);
        if (entry == null)
        {
            return null;
        }

        using var entryStream = entry.Open();
        using var reader = new StreamReader(entryStream, Encoding.UTF8, true);
        return reader.ReadToEnd();
    }

    public static void ExtractEntry(string jarPath, string entryPath, string destinationPath)
    {
        using var stream = File.OpenRead(jarPath);
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read);
        var entry = archive.GetEntry(entryPath)
                    ?? throw new FileNotFoundException($"Entry '{entryPath}' was not found in installer '{jarPath}'.", entryPath);

        Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);
        using var entryStream = entry.Open();
        using var output = File.Create(destinationPath);
        entryStream.CopyTo(output);
    }

    public static string? TryReadMainClassFromJar(string jarPath)
    {
        const string manifestPath = "META-INF/MANIFEST.MF";
        var manifestContent = ReadOptionalTextEntry(jarPath, manifestPath);
        if (string.IsNullOrWhiteSpace(manifestContent))
        {
            return null;
        }

        using var reader = new StringReader(manifestContent);
        while (reader.ReadLine() is { } line)
        {
            if (line.StartsWith("Main-Class:", StringComparison.OrdinalIgnoreCase))
            {
                return line["Main-Class:".Length..].Trim();
            }
        }

        return null;
    }
}
