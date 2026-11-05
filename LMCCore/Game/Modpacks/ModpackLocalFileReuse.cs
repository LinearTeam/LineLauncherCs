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

using LMC.Basic.Logging;

namespace LMCCore.Game.Modpacks;

internal static class ModpackLocalFileReuse
{
    private const int MaxDirectories = 128;
    private const int MaxHashCandidates = 4;
    private static readonly TimeSpan s_searchBudget = TimeSpan.FromMilliseconds(200);
    private static readonly Logger s_logger = new("ModpackLocalReuse");
    private static readonly StringComparer s_pathComparer = OperatingSystem.IsWindows()
        ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;

    internal static Task<IReadOnlyList<string>> GetSearchDirectoriesAsync(string currentRootPath,
        IReadOnlyList<string> managedRoots, CancellationToken cancellationToken) => Task.Run(() =>
    {
        var directories = new List<string>();
        var seen = new HashSet<string>(s_pathComparer);
        using var budget = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        budget.CancelAfter(s_searchBudget);
        try
        {
            // Include game roots first, then collect only immediate version directories once per install.
            foreach (var root in new[] { currentRootPath }.Concat(managedRoots).Take(MaxDirectories))
            {
                budget.Token.ThrowIfCancellationRequested();
                try
                {
                    if (string.IsNullOrWhiteSpace(root))
                        continue;
                    var path = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root));
                    if (Directory.Exists(path) && seen.Add(path))
                        directories.Add(path);
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
                { }
            }

            foreach (var root in directories.ToArray())
            {
                budget.Token.ThrowIfCancellationRequested();
                try
                {
                    foreach (var version in Directory.EnumerateDirectories(Path.Combine(root, "versions"), "*",
                                 new EnumerationOptions { AttributesToSkip = FileAttributes.ReparsePoint }))
                    {
                        budget.Token.ThrowIfCancellationRequested();
                        if (directories.Count >= MaxDirectories)
                            return (IReadOnlyList<string>)directories;
                        if (seen.Add(version))
                            directories.Add(version);
                    }
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                { }
            }
        }
        catch (OperationCanceledException)
        {
            cancellationToken.ThrowIfCancellationRequested();
        }
        cancellationToken.ThrowIfCancellationRequested();
        return (IReadOnlyList<string>)directories;
    }, cancellationToken);

    internal static Task<bool> TryCopyAsync(ModrinthModpackFile file, string destinationPath,
        IReadOnlyList<string> searchDirectories, CancellationToken cancellationToken) => Task.Run(async () =>
    {
        var relativePath = ModpackPaths.Normalize(file.Path).Replace('/', Path.DirectorySeparatorChar);
        var destination = Path.GetFullPath(destinationPath);
        var hashesChecked = 0;
        using var budget = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        budget.CancelAfter(s_searchBudget);
        try
        {
            foreach (var directory in searchDirectories.Take(MaxDirectories))
            {
                budget.Token.ThrowIfCancellationRequested();
                var candidate = Path.Combine(directory, relativePath);
                string? temporary = null;
                try
                {
                    var info = new FileInfo(candidate);
                    if (s_pathComparer.Equals(candidate, destination) || !info.Exists
                        || info.Length != file.FileSize || (info.Attributes & FileAttributes.ReparsePoint) != 0)
                        continue;
                    if (hashesChecked++ >= MaxHashCandidates)
                        break;

                    Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                    temporary = destination + $".reuse-{Guid.NewGuid():N}";
                    await using (var input = new FileStream(candidate, FileMode.Open, FileAccess.Read,
                                     FileShare.Read, 81920, FileOptions.Asynchronous | FileOptions.SequentialScan))
                    await using (var output = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write,
                                     FileShare.None, 81920, FileOptions.Asynchronous))
                        await ModpackPaths.CopyExactAsync(input, output, file.FileSize, budget.Token)
                            .ConfigureAwait(false);

                    // Validate the actual copy, so a changing source cannot publish unverified bytes.
                    await ModpackFileDownloader.ValidateFileAsync(temporary, file, budget.Token).ConfigureAwait(false);
                    budget.Token.ThrowIfCancellationRequested();
                    File.Move(temporary, destination, overwrite: true);
                    s_logger.Info($"Reused modpack file '{file.Path}' from '{candidate}'.");
                    return true;
                }
                catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException)
                {
                    s_logger.Debug($"Skipped local modpack file '{candidate}': {ex.Message}");
                }
                finally
                {
                    if (temporary != null)
                    {
                        try { File.Delete(temporary); }
                        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                        {
                            s_logger.Warn($"Could not remove local reuse temporary file '{temporary}': {ex.Message}");
                        }
                    }
                }
            }
        }
        catch (OperationCanceledException)
        {
            cancellationToken.ThrowIfCancellationRequested();
        }
        cancellationToken.ThrowIfCancellationRequested();
        return false;
    }, cancellationToken);
}
