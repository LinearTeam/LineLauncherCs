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

using System.Collections.Concurrent;
using LMCCore.Utils;

namespace LMCCore.Game.Mods.Local;

public sealed class LocalModManager
{
    private static readonly StringComparison s_pathComparison = OperatingSystem.IsWindows()
        ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

    public string ModsDirectory { get; }

    public LocalModManager(string versionDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(versionDirectory);
        ModsDirectory = Path.Combine(Path.GetFullPath(versionDirectory), "mods");
    }

    public Task<IReadOnlyList<LocalModFile>> ScanAsync(CancellationToken cancellationToken = default) =>
        Task.Run<IReadOnlyList<LocalModFile>>(() =>
        {
            if (!Directory.Exists(ModsDirectory))
                return Array.Empty<LocalModFile>();

            var files = Directory.EnumerateFiles(ModsDirectory, "*", new EnumerationOptions
            {
                RecurseSubdirectories = false,
                AttributesToSkip = FileAttributes.ReparsePoint | FileAttributes.Directory
            }).Where(IsModFile);
            var mods = new ConcurrentBag<LocalModFile>();
            Parallel.ForEach(files, new ParallelOptions
            {
                CancellationToken = cancellationToken,
                MaxDegreeOfParallelism = 4
            }, file => mods.Add(ModMetadataReader.Read(file)));
            return mods.OrderBy(mod => mod.Name, StringComparer.OrdinalIgnoreCase).ToArray();
        }, cancellationToken);

    public async Task ImportAsync(IEnumerable<string> paths, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(ModsDirectory);
        foreach (var path in paths)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!path.EndsWith(".jar", StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Only JAR files can be imported.");
            var destination = Path.Combine(ModsDirectory, Path.GetFileName(path));
            if (Path.GetFullPath(path).Equals(destination, s_pathComparison))
                continue;
            if (File.Exists(destination) || File.Exists(destination + ".disabled"))
                throw new IOException($"A mod with this file name already exists: {Path.GetFileName(path)}");

            var temporary = Path.Combine(ModsDirectory, $".import-{Guid.NewGuid():N}");
            try
            {
                await using (var source = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read,
                                 81920, FileOptions.Asynchronous | FileOptions.SequentialScan))
                await using (var target = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write,
                                 FileShare.None, 81920, FileOptions.Asynchronous))
                    await source.CopyToAsync(target, cancellationToken).ConfigureAwait(false);
                cancellationToken.ThrowIfCancellationRequested();
                File.Move(temporary, destination, overwrite: false);
            }
            finally
            {
                if (File.Exists(temporary))
                    File.Delete(temporary);
            }
        }
    }

    public async Task<LocalModFile> SetEnabledAsync(
        LocalModFile mod, bool enabled, CancellationToken cancellationToken = default) =>
        (await SetEnabledAsync([mod], enabled, cancellationToken).ConfigureAwait(false))[0];

    public Task<IReadOnlyList<LocalModFile>> SetEnabledAsync(
        IEnumerable<LocalModFile> mods, bool enabled, CancellationToken cancellationToken = default) =>
        Task.Run<IReadOnlyList<LocalModFile>>(() =>
        {
            var files = mods.ToArray();
            foreach (var mod in files)
                EnsureLocalFile(mod.FilePath);
            foreach (var mod in files)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (mod.IsEnabled == enabled)
                    continue;
                var target = enabled ? mod.EnabledFilePath : mod.FilePath + ".disabled";
                File.Move(mod.FilePath, target, overwrite: false);
            }
            return files.Select(mod => mod with
            {
                FilePath = enabled ? mod.EnabledFilePath : mod.EnabledFilePath + ".disabled",
                IsEnabled = enabled
            }).ToArray();
        }, cancellationToken);

    public Task DeleteAsync(LocalModFile mod, CancellationToken cancellationToken = default) =>
        DeleteAsync([mod], cancellationToken);

    public async Task DeleteAsync(
        IEnumerable<LocalModFile> mods, CancellationToken cancellationToken = default)
    {
        var files = mods.ToArray();
        foreach (var mod in files)
            EnsureLocalFile(mod.FilePath);
        foreach (var mod in files)
            await SystemFileOperations.DeleteFileAsync(mod.FilePath, cancellationToken).ConfigureAwait(false);
    }

    private void EnsureLocalFile(string path)
    {
        if (!string.Equals(Path.GetDirectoryName(Path.GetFullPath(path)), ModsDirectory,
                s_pathComparison) || !IsModFile(path))
            throw new ArgumentException("The file is not in this version's mods directory.", nameof(path));
    }

    private static bool IsModFile(string path) =>
        path.EndsWith(".jar", StringComparison.OrdinalIgnoreCase)
        || path.EndsWith(".jar.disabled", StringComparison.OrdinalIgnoreCase);
}
