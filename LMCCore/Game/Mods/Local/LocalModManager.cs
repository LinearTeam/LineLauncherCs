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
using System.ComponentModel;
using System.Runtime.InteropServices;
using LMCCore.Utils;

namespace LMCCore.Game.Mods.Local;

public sealed class LocalModManager
{
    private static readonly StringComparison s_pathComparison = OperatingSystem.IsWindows()
        ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> s_mutationLocks = new(
        OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);

    private sealed record ImportDestination(
        string FilePath, long Length, DateTime CreationTimeUtc, DateTime LastWriteTimeUtc, FileAttributes Attributes);

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
        ArgumentNullException.ThrowIfNull(paths);
        var result = await ImportCoreAsync(paths, (conflict, _) =>
            throw new IOException($"A mod with this file name already exists: {Path.GetFileName(conflict.SourcePath)}"),
            true, cancellationToken).ConfigureAwait(false);
        if (result.Errors.Count > 0)
            throw new IOException(result.Errors[0]);
    }

    public Task<ModImportResult> ImportAsync(
        IEnumerable<string> paths,
        Func<ModImportConflict, CancellationToken, Task<ModImportChoice>> resolveConflict,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(paths);
        ArgumentNullException.ThrowIfNull(resolveConflict);
        return ImportCoreAsync(paths, resolveConflict, false, cancellationToken);
    }

    private Task<ModImportResult> ImportCoreAsync(
        IEnumerable<string> paths,
        Func<ModImportConflict, CancellationToken, Task<ModImportChoice>> resolveConflict,
        bool stopOnError,
        CancellationToken cancellationToken) => MutateAsync(async markChanged =>
    {
        var imported = 0;
        var skipped = 0;
        var errors = new List<string>();
        var applyToAll = (ModImportDecision?)null;
        var cancelled = false;
        var seenSources = new HashSet<string>(s_mutationLocks.Comparer);
        EnsureModsDirectorySafe(create: true);
        foreach (var path in paths)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                if (!path.EndsWith(".jar", StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("Only JAR files can be imported.");
                var source = Path.GetFullPath(path);
                var destination = Path.Combine(ModsDirectory, Path.GetFileName(source));
                if (!seenSources.Add(source) || string.Equals(source, destination, s_pathComparison))
                {
                    skipped++;
                    continue;
                }

                EnsureOrdinaryJar(source);
                EnsureModsDirectorySafe(create: false);
                var approvedDestination = ReadImportDestination(destination);
                if (approvedDestination is not null)
                {
                    var choice = applyToAll.HasValue ? new ModImportChoice(applyToAll.Value)
                        : await resolveConflict(new ModImportConflict(source, approvedDestination.FilePath, false),
                            cancellationToken).ConfigureAwait(false);
                    cancellationToken.ThrowIfCancellationRequested();
                    if (choice.ApplyToAll)
                        applyToAll = choice.Decision;
                    if (choice.Decision == ModImportDecision.Cancel)
                    {
                        cancelled = true;
                        break;
                    }
                    if (choice.Decision == ModImportDecision.Skip)
                    {
                        skipped++;
                        continue;
                    }
                    if (choice.Decision != ModImportDecision.Replace)
                        throw new InvalidDataException("Invalid import conflict decision.");
                }

                cancellationToken.ThrowIfCancellationRequested();
                EnsureModsDirectorySafe(create: false);
                EnsureDestinationUnchanged(destination, approvedDestination);
                // A callback may have removed the source or replaced it with a link.
                EnsureOrdinaryJar(source);
                var temporary = Path.Combine(ModsDirectory, $".import-{Guid.NewGuid():N}.tmp");
                try
                {
                    await using (var input = new FileStream(source, FileMode.Open, FileAccess.Read, FileShare.Read,
                                     81920, FileOptions.Asynchronous | FileOptions.SequentialScan))
                    await using (var output = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write,
                                     FileShare.None, 81920, FileOptions.Asynchronous))
                    {
                        await input.CopyToAsync(output, cancellationToken).ConfigureAwait(false);
                        await output.FlushAsync(cancellationToken).ConfigureAwait(false);
                    }
                    cancellationToken.ThrowIfCancellationRequested();
                    EnsureModsDirectorySafe(create: false);
                    EnsureDestinationUnchanged(destination, approvedDestination);
                    File.Move(temporary, approvedDestination?.FilePath ?? destination,
                        overwrite: approvedDestination is not null);
                    imported++;
                    markChanged();
                }
                finally
                {
                    if (File.Exists(temporary))
                        File.Delete(temporary);
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException
                                      or ArgumentException)
            {
                if (stopOnError)
                    throw;
                errors.Add($"{Path.GetFileName(path)}: {ex.Message}");
            }
        }
        return new ModImportResult(imported, skipped, errors, cancelled);
    }, cancellationToken);

    public static event Action<string>? Changed;

    public async Task<LocalModFile> SetEnabledAsync(
        LocalModFile mod, bool enabled, CancellationToken cancellationToken = default) =>
        (await SetEnabledAsync([mod], enabled, cancellationToken).ConfigureAwait(false))[0];

    public Task<IReadOnlyList<LocalModFile>> SetEnabledAsync(
        IEnumerable<LocalModFile> mods, bool enabled, CancellationToken cancellationToken = default) =>
        MutateAsync<IReadOnlyList<LocalModFile>>(markChanged =>
        {
            var files = mods.ToArray();
            if (files.Length == 0)
                return Task.FromResult<IReadOnlyList<LocalModFile>>(files);
            foreach (var mod in files)
                EnsureLocalFile(mod.FilePath);
            EnsureModsDirectorySafe(create: false);
            foreach (var mod in files)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (mod.IsEnabled == enabled)
                    continue;
                var target = enabled ? mod.EnabledFilePath : mod.FilePath + ".disabled";
                File.Move(mod.FilePath, target, overwrite: false);
                markChanged();
            }
            var updated = files.Select(mod => mod with
            {
                FilePath = enabled ? mod.EnabledFilePath : mod.EnabledFilePath + ".disabled",
                IsEnabled = enabled
            }).ToArray();
            return Task.FromResult<IReadOnlyList<LocalModFile>>(updated);
        }, cancellationToken);

    public Task DeleteAsync(LocalModFile mod, CancellationToken cancellationToken = default) =>
        DeleteAsync([mod], cancellationToken);

    public async Task DeleteAsync(
        IEnumerable<LocalModFile> mods, CancellationToken cancellationToken = default)
    {
        await MutateAsync(async markChanged =>
        {
            var files = mods.ToArray();
            if (files.Length == 0)
                return true;
            foreach (var mod in files) EnsureLocalFile(mod.FilePath);
            EnsureModsDirectorySafe(create: false);
            foreach (var mod in files)
            {
                var existed = File.Exists(mod.FilePath);
                try
                {
                    await SystemFileOperations.DeleteFileAsync(mod.FilePath, cancellationToken).ConfigureAwait(false);
                }
                finally
                {
                    if (existed && !File.Exists(mod.FilePath))
                        markChanged();
                }
            }
            return true;
        }, cancellationToken).ConfigureAwait(false);
    }

    private SemaphoreSlim GetMutationLock() => s_mutationLocks.GetOrAdd(ModsDirectory, _ => new SemaphoreSlim(1, 1));

    private Task<T> MutateAsync<T>(Func<Action, Task<T>> action, CancellationToken token) => Task.Run(async () =>
    {
        var gate = GetMutationLock();
        await gate.WaitAsync(token).ConfigureAwait(false);
        var changed = false;
        try { return await action(() => changed = true).ConfigureAwait(false); }
        finally
        {
            gate.Release();
            if (changed)
                RaiseChanged();
        }
    }, token);

    private void EnsureModsDirectorySafe(bool create)
    {
        EnsureVersionDirectoryExists();
        FileAttributes attributes;
        try { attributes = File.GetAttributes(ModsDirectory); }
        catch (Exception ex) when (create && (ex is FileNotFoundException or DirectoryNotFoundException))
        {
            // Unlike Directory.CreateDirectory, these calls never recreate a missing version parent.
            var created = OperatingSystem.IsWindows()
                ? CreateDirectoryWindows(ModsDirectory, IntPtr.Zero)
                : CreateDirectoryUnix(ModsDirectory, 0x1FF) == 0;
            var error = Marshal.GetLastPInvokeError();
            if (!created && !Directory.Exists(ModsDirectory))
                throw new IOException("Unable to create the version's mods directory.", new Win32Exception(error));
            EnsureVersionDirectoryExists();
            attributes = File.GetAttributes(ModsDirectory);
        }
        if ((attributes & FileAttributes.Directory) == 0 || (attributes & FileAttributes.ReparsePoint) != 0)
            throw new IOException("The version's mods directory must be an ordinary directory, not a link.");
    }

    [DllImport("kernel32.dll", EntryPoint = "CreateDirectoryW", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CreateDirectoryWindows(string path, IntPtr securityAttributes);

    [DllImport("libc", EntryPoint = "mkdir", SetLastError = true)]
    private static extern int CreateDirectoryUnix([MarshalAs(UnmanagedType.LPUTF8Str)] string path, uint mode);

    private static ImportDestination? ReadImportDestination(string destination)
    {
        var enabled = ReadDestinationFile(destination);
        var disabled = ReadDestinationFile(destination + ".disabled");
        if (enabled is not null && disabled is not null)
            throw new IOException("Both enabled and disabled mod files exist; import was skipped.");
        return enabled ?? disabled;
    }

    private static ImportDestination? ReadDestinationFile(string path)
    {
        try
        {
            EnsureOrdinaryJar(path);
            var info = new FileInfo(path);
            return new ImportDestination(path, info.Length, info.CreationTimeUtc, info.LastWriteTimeUtc, info.Attributes);
        }
        catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException)
        {
            return null;
        }
    }

    private static void EnsureDestinationUnchanged(string destination, ImportDestination? approvedDestination)
    {
        if (ReadImportDestination(destination) != approvedDestination)
            throw new IOException("The destination mod changed during import; import was skipped. Try importing again.");
    }

    private void EnsureVersionDirectoryExists()
    {
        if (!Directory.Exists(Path.GetDirectoryName(ModsDirectory)!))
            throw new DirectoryNotFoundException("The game version directory no longer exists.");
    }

    private static void EnsureOrdinaryJar(string path)
    {
        var attributes = File.GetAttributes(path);
        if ((attributes & (FileAttributes.Directory | FileAttributes.ReparsePoint)) != 0)
            throw new InvalidDataException("The selected file must be an ordinary local JAR file.");
    }

    private void RaiseChanged()
    {
        foreach (Action<string> handler in Changed?.GetInvocationList() ?? [])
            try { handler(Path.GetDirectoryName(ModsDirectory)!); }
            catch { /* Subscribers must not break a completed mutation. */ }
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
