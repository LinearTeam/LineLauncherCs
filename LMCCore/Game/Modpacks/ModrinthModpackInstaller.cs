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
using LMC;
using LMC.Basic.Logging;
using LMCCore.Game.Download.Model;
using LMCCore.Game.Model.Loaders;
using LMCCore.Game.Versioning;

namespace LMCCore.Game.Modpacks;

public static class ModrinthModpackInstaller
{
    private static readonly Logger s_logger = new("ModpackInstallation");

    public static DownloadableGameVersion CreateRequest(ModrinthModpackInfo pack, string rootPath,
        string versionName, IReadOnlySet<string> optionalFilePaths)
    {
        ArgumentNullException.ThrowIfNull(pack);
        ArgumentNullException.ThrowIfNull(optionalFilePaths);
        var (minecraftVersion, loaderId, loaderVersion) = ModrinthModpackReader.ValidateIndex(pack.Index);
        if (!VersionNameValidator.IsValid(versionName))
            throw new ArgumentException("Invalid version name.", nameof(versionName));
        rootPath = Path.GetFullPath(rootPath);
        if (!Directory.Exists(rootPath))
            throw new DirectoryNotFoundException("The selected game directory no longer exists.");
        var target = Path.Combine(rootPath, "versions", versionName);
        if (Directory.Exists(target) || File.Exists(target))
            throw new IOException("A version with this name already exists.");
        var allPaths = pack.Index.Files.Select(file => file.Path).Concat(pack.OverridePaths).ToArray();
        foreach (var path in allPaths)
            ModpackPaths.EnsureNotReserved(path, versionName);
        ValidateOutputPaths(allPaths);
        var loader = loaderId switch
        {
            null => null,
            "fabric-loader" => new ModLoader { Type = ModLoaderType.Fabric, VersionId = loaderVersion! },
            "forge" => new ModLoader { Type = ModLoaderType.Forge, VersionId = loaderVersion! },
            _ => throw new NotSupportedException($"Unsupported loader: {loaderId}")
        };
        return new DownloadableGameVersion
        {
            RootPath = rootPath, VersionName = versionName, VersionId = minecraftVersion,
            Loaders = loader == null ? [] : [loader],
            Modpack = new ModrinthModpackInstallation(pack,
                new HashSet<string>(optionalFilePaths, StringComparer.Ordinal))
        };
    }

    internal static void ValidateOutputPaths(IEnumerable<string> paths)
    {
        var names = new Dictionary<string, string>(ModpackPaths.Comparer);
        foreach (var raw in paths)
        {
            var path = ModpackPaths.Normalize(raw);
            if (names.TryGetValue(path, out var previous) && previous != path)
                throw new InvalidDataException($"Conflicting platform file names: {previous}, {path}");
            names[path] = path;
        }
        foreach (var path in names.Keys)
        {
            var slash = path.LastIndexOf('/');
            while (slash >= 0)
            {
                if (names.ContainsKey(path[..slash]))
                    throw new InvalidDataException($"A modpack file is also used as a directory: {path[..slash]}");
                slash = path.LastIndexOf('/', slash - 1);
            }
        }
    }

    internal static Task PrepareArchiveAsync(ModrinthModpackInstallation installation,
        string cachedArchivePath, string contentDirectory, CancellationToken cancellationToken) =>
        Task.Run(async () =>
        {
            Directory.CreateDirectory(Path.GetDirectoryName(cachedArchivePath)!);
            await using (var input = new FileStream(installation.Pack.ArchivePath, FileMode.Open,
                FileAccess.Read, FileShare.Read, 81920, FileOptions.Asynchronous))
            await using (var output = new FileStream(cachedArchivePath, FileMode.CreateNew,
                FileAccess.Write, FileShare.None, 81920, FileOptions.Asynchronous))
            {
                ModpackPaths.EnsureSpace(cachedArchivePath, input.Length);
                await input.CopyToAsync(output, cancellationToken).ConfigureAwait(false);
            }
            await using (var cached = File.OpenRead(cachedArchivePath))
            {
                var hash = Convert.ToHexString(await SHA256.HashDataAsync(cached, cancellationToken)
                    .ConfigureAwait(false));
                if (!hash.Equals(installation.Pack.ArchiveSha256, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("The modpack archive changed after it was selected.");
            }
            using var archive = ZipFile.OpenRead(cachedArchivePath);
            long requiredBytes = 0;
            foreach (var file in installation.GetSelectedFiles())
                requiredBytes = checked(requiredBytes + file.FileSize);
            foreach (var entry in archive.Entries.Where(entry =>
                         ModrinthModpackReader.GetOverridePath(entry.FullName.TrimEnd('/')) != null))
                requiredBytes = checked(requiredBytes + entry.Length);
            ModpackPaths.EnsureSpace(contentDirectory, requiredBytes);
            Directory.CreateDirectory(contentDirectory);
        }, cancellationToken);

    internal static async Task DownloadFilesAsync(ModrinthModpackInstallation installation,
        string rootPath, string contentDirectory, IProgress<int> progress, CancellationToken cancellationToken)
    {
        var files = installation.GetSelectedFiles();
        var searchDirectories = files.Count == 0 ? [] : await ModpackLocalFileReuse.GetSearchDirectoriesAsync(
            rootPath, Current.Config?.ManagedGameRootPaths.ToArray() ?? [], cancellationToken).ConfigureAwait(false);
        var completed = 0;
        await Parallel.ForEachAsync(files, new ParallelOptions
        {
            MaxDegreeOfParallelism = 4,
            CancellationToken = cancellationToken
        }, async (file, token) =>
        {
            var destination = ModpackPaths.Resolve(contentDirectory, file.Path);
            if (!await ModpackLocalFileReuse.TryCopyAsync(file, destination, searchDirectories, token)
                    .ConfigureAwait(false))
                await ModpackFileDownloader.DownloadAsync(file, destination, token).ConfigureAwait(false);
            s_logger.Debug($"Installed modpack file: {file.Path}");
            progress.Report(Interlocked.Increment(ref completed) * 100 / Math.Max(files.Count, 1));
        }).ConfigureAwait(false);
        progress.Report(100);
    }

    internal static Task ExtractOverridesAsync(string cachedArchivePath, string contentDirectory,
        string versionName, CancellationToken cancellationToken) =>
        Task.Run(async () =>
        {
            using var archive = ZipFile.OpenRead(cachedArchivePath);
            foreach (var prefix in new[] { "overrides/", "client-overrides/" })
            {
                foreach (var entry in archive.Entries.Where(entry =>
                             entry.FullName.StartsWith(prefix, StringComparison.Ordinal)
                             && !entry.FullName.EndsWith('/')))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    ModpackPaths.ValidateEntry(entry);
                    var relative = entry.FullName[prefix.Length..];
                    ModpackPaths.EnsureNotReserved(relative, versionName);
                    var destination = ModpackPaths.Resolve(contentDirectory, relative);
                    Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                    await using var input = entry.Open();
                    await using var output = new FileStream(destination, FileMode.Create, FileAccess.Write,
                        FileShare.None, 81920, FileOptions.Asynchronous);
                    await ModpackPaths.CopyExactAsync(input, output, entry.Length, cancellationToken)
                        .ConfigureAwait(false);
                }
            }
        }, cancellationToken);
}
