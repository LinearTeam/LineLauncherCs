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

using System.Net;
using LMC.Basic.Logging;
using LMCCore.Utils;

namespace LMCCore.Game.Download.Vanilla.Batching;

public record BatchDownloadResult(
    int SuccessCount,
    int FailedCount,
    int SkippedCount
);

public class BatchDownloadOptions<TFile>
{
    public required IEnumerable<TFile> Files { get; init; }

    public int MaxConcurrency { get; init; } = 8;

    public required Func<TFile, string> GetSavePath { get; init; }

    public required Func<TFile, string> GetDownloadUrl { get; init; }

    public Func<TFile, long?>? GetFileSize { get; init; }

    public Func<TFile, string?>? GetHash { get; init; }

    public Func<TFile, IReadOnlyList<string>?>? GetHashes { get; init; }

    public Func<TFile, bool>? GetIgnoreNotFound { get; init; }

    public Func<TFile, string?>? GetDisplayName { get; init; }

    public bool SkipIfSizeMatches { get; init; } = true;

    public bool SkipIfHashMatches { get; init; } = true;

    public int MaxRetries { get; init; } = 3;
}

public static class BatchDownloader
{
    private readonly static Logger s_logger = new("BatchDownloader");

    private enum FileProcessResult
    {
        Downloaded,
        Skipped
    }

    public static Task<BatchDownloadResult> DownloadAsync<TFile>(
        BatchDownloadOptions<TFile> options,
        CancellationToken cancellationToken,
        IProgress<int>? progress = null)
    {
        return DownloadAsync(options, cancellationToken, progress, BatchDownloadRuntime.Default);
    }

    internal static async Task<BatchDownloadResult> DownloadAsync<TFile>(
        BatchDownloadOptions<TFile> options,
        CancellationToken cancellationToken,
        IProgress<int>? progress,
        BatchDownloadRuntime runtime)
    {
        var files = BatchDownloadPreparation.PrepareFiles(options);
        var totalCount = files.Count;

        if (totalCount == 0)
        {
            return new BatchDownloadResult(0, 0, 0);
        }

        runtime.PreCreateDirectories(BatchDownloadPreparation.CollectDirectories(files));

        var nextFileIndex = -1;
        var workerCount = Math.Min(Math.Max(options.MaxConcurrency, 1), totalCount);
        var progressTracker = new BatchDownloadProgressTracker(totalCount, progress);

        var workers = Enumerable.Range(0, workerCount).Select(_ => Task.Run(async () =>
        {
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var fileIndex = Interlocked.Increment(ref nextFileIndex);
                if (fileIndex >= totalCount)
                {
                    break;
                }

                try
                {
                    var result = await ProcessFileAsync(options, files[fileIndex], runtime, cancellationToken);
                    if (result == FileProcessResult.Skipped)
                    {
                        progressTracker.RecordSkipped();
                    }
                    else
                    {
                        progressTracker.RecordDownloaded();
                    }
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    s_logger.Error(ex, "Download failed.");
                    progressTracker.RecordFailed();
                }
            }
        }, cancellationToken));

        try
        {
            await Task.WhenAll(workers);
        }
        catch (OperationCanceledException)
        {
            s_logger.Debug("Download canceled.");
            throw;
        }

        if (!cancellationToken.IsCancellationRequested)
        {
            progressTracker.ReportCompleted();
        }

        return progressTracker.CreateResult();
    }

    private static async Task<FileProcessResult> ProcessFileAsync<TFile>(
        BatchDownloadOptions<TFile> options,
        PreparedBatchFile<TFile> file,
        BatchDownloadRuntime runtime,
        CancellationToken cancellationToken)
    {
        if (File.Exists(file.SavePath))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var skipReason = BatchDownloadSkipEvaluator.GetSkipReason(file, options, runtime, cancellationToken);
            if (skipReason != null)
            {
                s_logger.Debug($"{file.DisplayName} already exists ({skipReason}), skipping.");
                return FileProcessResult.Skipped;
            }
        }

        s_logger.Debug($"Downloading {file.DisplayName}");

        var tempPath = CreateTemporaryDownloadPath(file.SavePath);
        try
        {
            await DownloadAndValidateFileAsync(file, tempPath, options, runtime, cancellationToken);
            PromoteDownloadedFile(tempPath, file.SavePath);
            return FileProcessResult.Downloaded;
        }
        catch (Exception ex) when (file.IgnoreNotFound && IsNotFoundDownloadFailure(ex))
        {
            s_logger.Info($"{file.DisplayName} returned 404 from an optional source and will be skipped.");
            return FileProcessResult.Skipped;
        }
        finally
        {
            TryDeleteFile(tempPath);
        }
    }

    internal static void PreCreateDirectories(IEnumerable<string> directories)
    {
        foreach (var dir in directories)
        {
            try
            {
                Directory.CreateDirectory(dir);
            }
            catch
            {
                // ignored
            }
        }
    }

    internal static string ComputeSha1Fast(string filePath, CancellationToken cancellationToken)
    {
        var fileInfo = new FileInfo(filePath);
        var bufferSize = GetHashBufferSize(fileInfo.Length);

        using var sha1 = System.Security.Cryptography.SHA1.Create();
        using var stream = new FileStream(filePath, new FileStreamOptions
        {
            Access = FileAccess.Read,
            Mode = FileMode.Open,
            Share = FileShare.Read,
            BufferSize = bufferSize,
            Options = FileOptions.SequentialScan
        });

        var buffer = new byte[bufferSize];
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var bytesRead = stream.Read(buffer, 0, buffer.Length);
            if (bytesRead <= 0)
            {
                break;
            }

            sha1.TransformBlock(buffer, 0, bytesRead, buffer, 0);
        }

        sha1.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
        return BitConverter.ToString(sha1.Hash!).Replace("-", "").ToLowerInvariant();
    }

    private static int GetHashBufferSize(long fileLength)
    {
        return fileLength switch
        {
            <= 64 * 1024 => 4 * 1024,
            <= 512 * 1024 => 16 * 1024,
            <= 4 * 1024 * 1024 => 64 * 1024,
            <= 32 * 1024 * 1024 => 256 * 1024,
            <= 128 * 1024 * 1024 => 512 * 1024,
            _ => 1024 * 1024
        };
    }

    internal static async Task DownloadFileAsync(string url, string savePath, CancellationToken cancellationToken, int maxRetries = 3)
    {
        Exception? lastException = null;
        var useDirectWrite = savePath.EndsWith(".download", StringComparison.OrdinalIgnoreCase);
        var tempPath = useDirectWrite ? savePath : CreateTemporaryDownloadPath(savePath);

        for (var retry = 0; retry <= maxRetries; retry++)
        {
            try
            {
                var response = await HttpUtils.CreateRequest(url)
                    .WithRetry(3)
                    .WithRetryDelay(1000)
                    .GetAsync(cancellationToken);

                response.EnsureSuccessStatusCode();

                await using var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken);
                await using var fileStream = File.Create(tempPath);
                await contentStream.CopyToAsync(fileStream, cancellationToken);
                await fileStream.FlushAsync(cancellationToken);

                if (!useDirectWrite)
                {
                    PromoteDownloadedFile(tempPath, savePath);
                }

                return;
            }
            catch (Exception ex) when (retry < maxRetries && !cancellationToken.IsCancellationRequested)
            {
                lastException = ex;
                s_logger.Debug($"Download failed (retry {retry + 1}/{maxRetries + 1}): {ex.Message}");
                TryDeleteFile(tempPath);
                await Task.Delay(500 * (retry + 1), cancellationToken);
            }
        }

        TryDeleteFile(tempPath);
        throw lastException ?? new InvalidOperationException($"Failed to download {url} after {maxRetries + 1} attempts.");
    }

    private static async Task DownloadAndValidateFileAsync<TFile>(
        PreparedBatchFile<TFile> file,
        string tempPath,
        BatchDownloadOptions<TFile> options,
        BatchDownloadRuntime runtime,
        CancellationToken cancellationToken)
    {
        await runtime.DownloadFileAsync(file.DownloadUrl, tempPath, cancellationToken, options.MaxRetries);

        if (string.IsNullOrEmpty(file.Hash) && (file.Hashes == null || file.Hashes.Count == 0))
        {
            return;
        }

        cancellationToken.ThrowIfCancellationRequested();
        var actualHash = runtime.ComputeSha1(tempPath, cancellationToken);
        if (BatchDownloadSkipEvaluator.MatchesAnyHash(actualHash, file.Hash, file.Hashes))
        {
            return;
        }

        s_logger.Warn($"{file.DisplayName} failed hash validation after download, retrying once.");
        TryDeleteFile(tempPath);
        await runtime.DownloadFileAsync(file.DownloadUrl, tempPath, cancellationToken, options.MaxRetries);

        cancellationToken.ThrowIfCancellationRequested();
        actualHash = runtime.ComputeSha1(tempPath, cancellationToken);
        if (BatchDownloadSkipEvaluator.MatchesAnyHash(actualHash, file.Hash, file.Hashes))
        {
            return;
        }

        s_logger.Error($"{file.DisplayName} still failed hash validation after retry.");
        throw new InvalidOperationException($"SHA1 mismatch for {file.DisplayName}");
    }

    private static string CreateTemporaryDownloadPath(string savePath)
    {
        var directory = Path.GetDirectoryName(savePath);
        var fileName = Path.GetFileName(savePath);
        var tempFileName = $"{fileName}.{Guid.NewGuid():N}.download";
        return string.IsNullOrWhiteSpace(directory)
            ? tempFileName
            : Path.Combine(directory, tempFileName);
    }

    private static void PromoteDownloadedFile(string sourcePath, string destinationPath)
    {
        var directory = Path.GetDirectoryName(destinationPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        if (File.Exists(destinationPath))
        {
            File.Replace(sourcePath, destinationPath, destinationBackupFileName: null, ignoreMetadataErrors: true);
            return;
        }

        File.Move(sourcePath, destinationPath);
    }

    private static void TryDeleteFile(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
            // ignored
        }
    }

    private static bool IsNotFoundDownloadFailure(Exception ex)
    {
        if (ex is HttpRequestException requestException &&
            requestException.StatusCode == HttpStatusCode.NotFound)
        {
            return true;
        }

        return ex.InnerException != null && IsNotFoundDownloadFailure(ex.InnerException);
    }
}
