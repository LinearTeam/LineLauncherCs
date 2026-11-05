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
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace LMCCore.Game.Modpacks;

public static class ModrinthModpackReader
{
    private static readonly JsonSerializerOptions s_options = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
    private const long MaxIndexSize = 16 * 1024 * 1024;

    public static Task<ModrinthModpackInfo> ReadAsync(
        string archivePath, CancellationToken cancellationToken = default) =>
        Task.Run(async () =>
        {
            var fullPath = Path.GetFullPath(archivePath);
            await using var input = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read,
                81920, FileOptions.Asynchronous | FileOptions.SequentialScan);
            using var archive = new ZipArchive(input, ZipArchiveMode.Read, leaveOpen: true);
            var names = new HashSet<string>(ModpackPaths.Comparer);
            foreach (var entry in archive.Entries)
            {
                cancellationToken.ThrowIfCancellationRequested();
                ModpackPaths.ValidateEntry(entry);
                if (!names.Add(ModpackPaths.Normalize(entry.FullName.TrimEnd('/'))))
                    throw new InvalidDataException($"Duplicate ZIP path: {entry.FullName}");
            }
            var indexEntry = archive.GetEntry("modrinth.index.json")
                ?? throw new InvalidDataException("This archive is not a Modrinth modpack.");
            if (indexEntry.Length > MaxIndexSize)
                throw new InvalidDataException("Modrinth index is too large.");
            await using var indexStream = indexEntry.Open();
            using var indexBytes = new MemoryStream();
            await ModpackPaths.CopyExactAsync(indexStream, indexBytes, indexEntry.Length, cancellationToken);
            var bytes = indexBytes.ToArray().AsMemory();
            if (bytes.Length >= 3 && bytes.Span[0] == 0xEF && bytes.Span[1] == 0xBB && bytes.Span[2] == 0xBF)
                bytes = bytes[3..];
            using var document = JsonDocument.Parse(bytes);
            ValidateJsonProperties(document.RootElement);
            var index = document.RootElement.Deserialize<ModrinthModpackIndex>(s_options)
                ?? throw new InvalidDataException("Modrinth index is not a JSON object.");
            var (minecraft, loader, loaderVersion) = ValidateIndex(index);
            input.Position = 0;
            var hash = Convert.ToHexString(await SHA256.HashDataAsync(input, cancellationToken));
            return new ModrinthModpackInfo(fullPath, hash, index, minecraft, loader, loaderVersion)
            {
                OverridePaths = archive.Entries.Where(entry => !entry.FullName.EndsWith('/'))
                    .Select(entry => GetOverridePath(entry.FullName)).OfType<string>().ToArray()
            };
        }, cancellationToken);

    internal static (string Minecraft, string? Loader, string? LoaderVersion) ValidateIndex(ModrinthModpackIndex index)
    {
        if (index.FormatVersion != 1 || index.Game != "minecraft")
            throw new NotSupportedException("Only Minecraft Modrinth packs using formatVersion 1 are supported.");
        if (string.IsNullOrWhiteSpace(index.Name) || string.IsNullOrWhiteSpace(index.VersionId)
            || index.Files == null || index.Dependencies == null)
            throw new InvalidDataException("Modrinth index is missing required fields.");
        if (!index.Dependencies.TryGetValue("minecraft", out var minecraft) || !IsVersionToken(minecraft))
            throw new InvalidDataException("The modpack must specify a Minecraft version.");
        string? loader = null;
        string? loaderVersion = null;
        foreach (var dependency in index.Dependencies)
        {
            if (!IsVersionToken(dependency.Value))
                throw new InvalidDataException($"Invalid dependency version: {dependency.Key}");
            if (dependency.Key == "minecraft")
                continue;
            if (dependency.Key is not ("fabric-loader" or "forge"))
                throw new NotSupportedException($"Unsupported modpack dependency: {dependency.Key}");
            if (loader != null)
                throw new InvalidDataException("The modpack specifies conflicting mod loaders.");
            loader = dependency.Key;
            loaderVersion = dependency.Value;
        }
        var paths = new HashSet<string>(ModpackPaths.Comparer);
        foreach (var file in index.Files)
        {
            if (file == null || !paths.Add(ModpackPaths.Normalize(file.Path)))
                throw new InvalidDataException("The modpack contains duplicate file paths.");
            if (file.FileSize < 0 || file.Hashes == null
                || !HasHash(file.Hashes, "sha1", 40) || !HasHash(file.Hashes, "sha512", 128)
                || file.Downloads == null || file.Downloads.Count == 0)
                throw new InvalidDataException($"Incomplete file metadata: {file.Path}");
            if (file.Env != null && (!IsEnvironment(file.Env.Client) || !IsEnvironment(file.Env.Server)))
                throw new InvalidDataException($"Invalid file environment: {file.Path}");
            foreach (var url in file.Downloads)
                ModpackFileDownloader.ValidateUrl(url, isRedirect: false);
        }
        return (minecraft, loader, loaderVersion);
    }

    internal static string? GetOverridePath(string entryName)
    {
        foreach (var prefix in new[] { "overrides/", "client-overrides/" })
            if (entryName.StartsWith(prefix, StringComparison.Ordinal))
                return ModpackPaths.Normalize(entryName[prefix.Length..]);
        return null;
    }

    private static void ValidateJsonProperties(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var property in element.EnumerateObject())
            {
                if (!names.Add(property.Name))
                    throw new InvalidDataException($"Duplicate JSON property: {property.Name}");
                ValidateJsonProperties(property.Value);
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
                ValidateJsonProperties(item);
        }
    }

    private static bool IsEnvironment(string value) => value is "required" or "optional" or "unsupported";

    private static bool HasHash(Dictionary<string, string> hashes, string name, int length) =>
        hashes.TryGetValue(name, out var hash) && hash != null && hash.Length == length && hash.All(Uri.IsHexDigit);

    private static bool IsVersionToken(string? value) =>
        value != null && Regex.IsMatch(value, @"\A[A-Za-z0-9][A-Za-z0-9._+\-]*\z",
            RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));
}
