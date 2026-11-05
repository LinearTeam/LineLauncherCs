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

namespace LMCCore.Game.Versioning;

// The staging root is on the game drive, so publishing never exposes a half-copied version.
internal static class NewVersionPublisher
{
    private static readonly SemaphoreSlim s_publishLock = new(1, 1);
    private static readonly Logger s_logger = new("VersionPublisher");

    public static async Task PublishAsync(string rootPath, string versionName,
        Func<string, CancellationToken, Task> buildAsync, Action afterPublish, CancellationToken cancellationToken)
    {
        if (!VersionNameValidator.IsValid(versionName))
            throw new ArgumentException("Invalid version name.", nameof(versionName));
        var destination = Path.Combine(rootPath, "versions", versionName);
        var stagingRoot = Path.Combine(rootPath, $".lmc-install-{Guid.NewGuid():N}");
        var published = false;
        await s_publishLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (!Directory.Exists(rootPath))
                throw new DirectoryNotFoundException(rootPath);
            if (Directory.Exists(destination) || File.Exists(destination))
                throw new IOException("The target version already exists.");
            Directory.CreateDirectory(stagingRoot);
            await buildAsync(stagingRoot, cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            Directory.Move(Path.Combine(stagingRoot, "versions", versionName), destination);
            published = true;
            afterPublish();
        }
        catch
        {
            if (published)
                TryCleanup(destination);
            throw;
        }
        finally
        {
            TryCleanup(stagingRoot);
            s_publishLock.Release();
        }
    }

    private static void TryCleanup(string directory)
    {
        try
        {
            if (Directory.Exists(directory))
                Directory.Delete(directory, recursive: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            s_logger.Warn($"Failed to remove installation staging directory '{directory}': {ex.Message}");
        }
    }
}
