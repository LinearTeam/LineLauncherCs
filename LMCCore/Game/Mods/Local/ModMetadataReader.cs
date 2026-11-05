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
using System.Text.Json;
using System.Text.RegularExpressions;

namespace LMCCore.Game.Mods.Local;

internal static class ModMetadataReader
{
    public static LocalModFile Read(string path)
    {
        var enabled = path.EndsWith(".jar", StringComparison.OrdinalIgnoreCase);
        var name = Path.GetFileName(enabled ? path : path[..^9]);
        name = Path.GetFileNameWithoutExtension(name);
        try
        {
            using var archive = ZipFile.OpenRead(path);
            if (archive.GetEntry("fabric.mod.json") is { } fabric)
            {
                using var doc = JsonDocument.Parse(ReadText(fabric));
                return new LocalModFile(path,
                    GetString(doc.RootElement, "name") ?? GetString(doc.RootElement, "id") ?? name,
                    GetString(doc.RootElement, "version"), "Fabric", enabled, false);
            }

            if (archive.GetEntry("quilt.mod.json") is { } quilt)
            {
                using var doc = JsonDocument.Parse(ReadText(quilt));
                var loader = doc.RootElement.GetProperty("quilt_loader");
                var title = loader.TryGetProperty("metadata", out var metadata)
                    ? GetString(metadata, "name") : null;
                return new LocalModFile(path, title ?? GetString(loader, "id") ?? name,
                    GetString(loader, "version"), "Quilt", enabled, false);
            }

            var toml = archive.GetEntry("META-INF/neoforge.mods.toml") ?? archive.GetEntry("META-INF/mods.toml");
            if (toml != null)
            {
                var text = ReadText(toml);
                return new LocalModFile(path, GetTomlString(text, "displayName") ?? name,
                    GetTomlString(text, "version"),
                    toml.FullName.Contains("neoforge", StringComparison.Ordinal) ? "NeoForge" : "Forge",
                    enabled, false);
            }

            if (archive.GetEntry("mcmod.info") is { } legacy)
            {
                using var doc = JsonDocument.Parse(ReadText(legacy));
                var mods = doc.RootElement.ValueKind == JsonValueKind.Array
                    ? doc.RootElement : doc.RootElement.GetProperty("modList");
                if (mods.GetArrayLength() > 0)
                    return new LocalModFile(path, GetString(mods[0], "name") ?? name,
                        GetString(mods[0], "version"), "Forge", enabled, false);
            }

            return new LocalModFile(path, name, null, null, enabled, false);
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or JsonException
                                   or InvalidOperationException or KeyNotFoundException or UnauthorizedAccessException
                                   or RegexMatchTimeoutException)
        {
            return new LocalModFile(path, name, null, null, enabled, true);
        }
    }

    private static string ReadText(ZipArchiveEntry entry)
    {
        // Read only small metadata entries, never the mod's classes or nested JARs.
        if (entry.Length > 1024 * 1024)
            throw new InvalidDataException("Mod metadata exceeds the size limit.");
        using var reader = new StreamReader(entry.Open());
        return reader.ReadToEnd();
    }

    private static string? GetString(JsonElement element, string key) =>
        element.TryGetProperty(key, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() : null;

    private static string? GetTomlString(string text, string key)
    {
        var match = Regex.Match(text, @"(?m)^\s*" + Regex.Escape(key) + """\s*=\s*["']([^"'\r\n]+)["']""",
            RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));
        return match.Success && !match.Groups[1].Value.Contains("${", StringComparison.Ordinal)
            ? match.Groups[1].Value : null;
    }
}
