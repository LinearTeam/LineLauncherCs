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
using LMCCore.Utils;

namespace LMCCore.Game.Download.Installation;

internal static class InstallationFileDownloader
{
    private static readonly Logger s_logger = new("InstallationFileDownloader");

    public static async Task DownloadFileWithFallbackAsync(
        IEnumerable<string> candidateUrls,
        string savePath,
        CancellationToken cancellationToken,
        int maxRetriesPerSource = 3)
    {
        ArgumentNullException.ThrowIfNull(candidateUrls);
        ArgumentException.ThrowIfNullOrWhiteSpace(savePath);

        var urls = candidateUrls
            .Where(url => !string.IsNullOrWhiteSpace(url))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (urls.Length == 0)
        {
            throw new ArgumentException("At least one candidate url is required.", nameof(candidateUrls));
        }

        Exception? lastException = null;

        foreach (var url in urls)
        {
            for (var retry = 0; retry <= maxRetriesPerSource; retry++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    using var response = await HttpUtils.CreateRequest(url)
                        .WithRetry(1)
                        .GetAsync(cancellationToken);

                    response.EnsureSuccessStatusCode();

                    await using var responseStream = await response.Content.ReadAsStreamAsync(cancellationToken);
                    await using var fileStream = new FileStream(savePath, new FileStreamOptions
                    {
                        Access = FileAccess.Write,
                        Mode = FileMode.Create,
                        Share = FileShare.None,
                        Options = FileOptions.Asynchronous
                    });
                    await responseStream.CopyToAsync(fileStream, cancellationToken);
                    return;
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
                {
                    lastException = ex;
                    s_logger.Debug($"Failed to download '{url}' (attempt {retry + 1}/{maxRetriesPerSource + 1}): {ex.Message}");

                    TryDeleteIncompleteFile(savePath);

                    var hasNextRetry = retry < maxRetriesPerSource;
                    var isLastUrl = string.Equals(url, urls[^1], StringComparison.OrdinalIgnoreCase);
                    if (!hasNextRetry && !isLastUrl)
                    {
                        s_logger.Warn($"Exhausted retries for '{url}', switching to next download source.");
                        break;
                    }

                    if (hasNextRetry)
                    {
                        await Task.Delay(500 * (retry + 1), cancellationToken);
                    }
                }
            }
        }

        throw lastException ?? new InvalidOperationException("Failed to download file from all candidate urls.");
    }

    private static void TryDeleteIncompleteFile(string savePath)
    {
        if (!File.Exists(savePath))
        {
            return;
        }

        try
        {
            File.Delete(savePath);
        }
        catch
        {
            // ignored
        }
    }
}
