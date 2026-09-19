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

using LMCCore.Game.Download.Vanilla.Batching;

namespace LMCCore.Game.Launching.Steps.Resources;

internal static class GameLaunchFileIntegrityHelper
{
    public static GameLaunchFileCheckResult CheckFile(
        string path,
        string? sha1,
        long? size,
        CancellationToken cancellationToken)
    {
        return CheckFile(path, sha1, null, size, cancellationToken);
    }

    public static GameLaunchFileCheckResult CheckFile(
        string path,
        string? sha1,
        IReadOnlyCollection<string>? validHashes,
        long? size,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        if (!File.Exists(path))
        {
            return new GameLaunchFileCheckResult(false, false, "File not found");
        }

        var acceptedHashes = BuildAcceptedHashes(sha1, validHashes);
        if (acceptedHashes.Count > 0)
        {
            var actualHash = BatchDownloader.ComputeSha1Fast(path, cancellationToken);
            return acceptedHashes.Contains(actualHash, StringComparer.OrdinalIgnoreCase)
                ? new GameLaunchFileCheckResult(true, true, "SHA1 matched")
                : new GameLaunchFileCheckResult(
                    true,
                    false,
                    $"SHA1 mismatch: expected one of [{string.Join(", ", acceptedHashes)}], actual {actualHash}");
        }

        if (size.HasValue)
        {
            var actualSize = new FileInfo(path).Length;
            return actualSize == size.Value
                ? new GameLaunchFileCheckResult(true, true, "Size matched")
                : new GameLaunchFileCheckResult(true, false, $"Size mismatch: expected {size.Value}, actual {actualSize}");
        }

        return new GameLaunchFileCheckResult(true, true, "Exists");
    }

    private static List<string> BuildAcceptedHashes(string? sha1, IReadOnlyCollection<string>? validHashes)
    {
        var hashes = new List<string>();
        if (!string.IsNullOrWhiteSpace(sha1))
        {
            hashes.Add(sha1);
        }

        if (validHashes != null)
        {
            foreach (var hash in validHashes)
            {
                if (!string.IsNullOrWhiteSpace(hash) &&
                    !hashes.Contains(hash, StringComparer.OrdinalIgnoreCase))
                {
                    hashes.Add(hash);
                }
            }
        }

        return hashes;
    }
}
