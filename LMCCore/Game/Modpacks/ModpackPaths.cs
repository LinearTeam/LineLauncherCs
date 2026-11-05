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

using System.Buffers;
using System.IO.Compression;
using System.Text;

namespace LMCCore.Game.Modpacks;

internal static class ModpackPaths
{
    internal static readonly StringComparer Comparer = StringComparer.OrdinalIgnoreCase;

    public static string Normalize(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || path.Contains('\\') || path.StartsWith('/'))
            throw new InvalidDataException($"Invalid modpack path: {path}");
        var parts = path.Split('/');
        foreach (var part in parts)
        {
            var deviceName = part.Split('.')[0];
            if (string.IsNullOrEmpty(part) || part is "." or ".." || part.EndsWith('.') || part.EndsWith(' ')
                || part.Any(c => c < 32 || "<>:\"|?*".Contains(c))
                || deviceName.Equals("CON", StringComparison.OrdinalIgnoreCase)
                || deviceName.Equals("PRN", StringComparison.OrdinalIgnoreCase)
                || deviceName.Equals("AUX", StringComparison.OrdinalIgnoreCase)
                || deviceName.Equals("NUL", StringComparison.OrdinalIgnoreCase)
                || (deviceName.Length == 4 && "123456789\u00b9\u00b2\u00b3".Contains(deviceName[3])
                    && (deviceName.StartsWith("COM", StringComparison.OrdinalIgnoreCase)
                        || deviceName.StartsWith("LPT", StringComparison.OrdinalIgnoreCase))))
                throw new InvalidDataException($"Invalid modpack path: {path}");
        }
        return string.Join('/', parts).Normalize(NormalizationForm.FormC);
    }

    public static string Resolve(string root, string relativePath)
    {
        var path = Normalize(relativePath);
        var normalizedRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root));
        var result = Path.GetFullPath(Path.Combine(normalizedRoot, path.Replace('/', Path.DirectorySeparatorChar)));
        if (!result.StartsWith(normalizedRoot + Path.DirectorySeparatorChar,
                OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))
            throw new InvalidDataException($"Modpack path escapes the instance: {relativePath}");
        return result;
    }

    public static void ValidateEntry(ZipArchiveEntry entry)
    {
        Normalize(entry.FullName.TrimEnd('/'));
        var unixType = (entry.ExternalAttributes >> 16) & 0xF000;
        if (unixType == 0xA000 || (entry.ExternalAttributes & (int)FileAttributes.ReparsePoint) != 0)
            throw new InvalidDataException($"Links are not allowed in modpacks: {entry.FullName}");
    }

    public static void EnsureNotReserved(string relativePath, string versionName)
    {
        var path = Normalize(relativePath);
        if (Comparer.Equals(path, Normalize(versionName + ".json"))
            || Comparer.Equals(path, Normalize(versionName + ".jar"))
            || path.Equals("LMC", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("LMC/", StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException($"Modpack attempts to replace launcher metadata: {relativePath}");
    }

    public static async Task CopyExactAsync(Stream source, Stream destination, long expectedSize,
        CancellationToken cancellationToken)
    {
        var buffer = ArrayPool<byte>.Shared.Rent(81920);
        try
        {
            long total = 0;
            int read;
            while ((read = await source.ReadAsync(buffer.AsMemory(), cancellationToken).ConfigureAwait(false)) != 0)
            {
                total = checked(total + read);
                if (total > expectedSize)
                    throw new InvalidDataException("File exceeds its declared size.");
                await destination.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
            }
            if (total != expectedSize)
                throw new InvalidDataException("File does not match its declared size.");
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    public static void EnsureSpace(string directory, long requiredBytes)
    {
        var root = Path.GetPathRoot(Path.GetFullPath(directory));
        if (root == null)
            return;
        var drive = new DriveInfo(root);
        if (drive.IsReady && drive.AvailableFreeSpace < requiredBytes)
            throw new IOException("Not enough disk space to install the modpack.");
    }
}
