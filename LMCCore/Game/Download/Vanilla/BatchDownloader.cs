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

using LMCCore.Utils;
using LMC.Basic.Logging;

namespace LMCCore.Game.Download.Vanilla;

/// <summary>
/// 批量下载结果
/// </summary>
public record BatchDownloadResult(
    int SuccessCount,
    int FailedCount,
    int SkippedCount
);

/// <summary>
/// 批量下载选项
/// </summary>
/// <typeparam name="TFile">文件类型</typeparam>
public class BatchDownloadOptions<TFile>
{
    /// <summary>
    /// 文件集合
    /// </summary>
    public required IEnumerable<TFile> Files { get; init; }
    
    /// <summary>
    /// 最大并发数
    /// </summary>
    public int MaxConcurrency { get; init; } = 8;
    
    /// <summary>
    /// 获取保存路径
    /// </summary>
    public required Func<TFile, string> GetSavePath { get; init; }
    
    /// <summary>
    /// 获取下载URL
    /// </summary>
    public required Func<TFile, string> GetDownloadUrl { get; init; }
    
    /// <summary>
    /// 获取文件大小（返回null表示未知）
    /// </summary>
    public Func<TFile, long?>? GetFileSize { get; init; }
    
    /// <summary>
    /// 获取SHA1校验码（返回null表示不校验）
    /// </summary>
    public Func<TFile, string?>? GetHash { get; init; }
    
    /// <summary>
    /// 获取日志显示名称
    /// </summary>
    public Func<TFile, string?>? GetDisplayName { get; init; }
    
    /// <summary>
    /// 是否跳过已有且大小匹配的文件（默认true）
    /// </summary>
    public bool SkipIfSizeMatches { get; init; } = true;
    
    /// <summary>
    /// 是否跳过已有且校验通过的文件（默认true）
    /// </summary>
    public bool SkipIfHashMatches { get; init; } = true;
    
    /// <summary>
    /// 下载失败重试次数（默认3次）
    /// </summary>
    public int MaxRetries { get; init; } = 3;
}

/// <summary>
/// 批量下载器 - 提供通用的批量文件下载能力
/// </summary>
public static class BatchDownloader
{
    private readonly static Logger s_logger = new("BatchDownloader");

    private enum FileProcessResult
    {
        Downloaded,
        Skipped
    }

    /// <summary>
    /// 批量下载文件
    /// </summary>
    public async static Task<BatchDownloadResult> DownloadAsync<TFile>(
        BatchDownloadOptions<TFile> options,
        CancellationToken cancellationToken,
        IProgress<int>? progress = null)
    {
        return await DownloadAsync(options, cancellationToken, progress, BatchDownloadRuntime.Default);
    }

    internal async static Task<BatchDownloadResult> DownloadAsync<TFile>(
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
                    s_logger.Error(ex, "下载失败");
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
            s_logger.Debug("下载已被取消");
            throw;
        }

        // 确保报告最终进度
        if (!cancellationToken.IsCancellationRequested)
        {
            progressTracker.ReportCompleted();
        }
        return progressTracker.CreateResult();
    }

    async private static Task<FileProcessResult> ProcessFileAsync<TFile>(
        BatchDownloadOptions<TFile> options,
        PreparedBatchFile<TFile> file,
        BatchDownloadRuntime runtime,
        CancellationToken cancellationToken)
    {
        // 检查是否需要跳过已有文件
        if (File.Exists(file.SavePath))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var skipReason = BatchDownloadSkipEvaluator.GetSkipReason(file, options, runtime, cancellationToken);
            if (skipReason != null)
            {
                s_logger.Debug($"{file.DisplayName} 已存在（{skipReason}），跳过下载");
                return FileProcessResult.Skipped;
            }
        }

        s_logger.Debug($"下载 {file.DisplayName}");

        // 下载文件
        await runtime.DownloadFileAsync(file.DownloadUrl, file.SavePath, cancellationToken, options.MaxRetries);

        // 下载后校验
        if (!string.IsNullOrEmpty(file.Hash))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var actualHash = runtime.ComputeSha1(file.SavePath, cancellationToken);
            if (actualHash != file.Hash)
            {
                s_logger.Warn($"{file.DisplayName} 校验失败，预期 {file.Hash}，实际 {actualHash}，重新下载中");
                File.Delete(file.SavePath);
                await runtime.DownloadFileAsync(file.DownloadUrl, file.SavePath, cancellationToken, options.MaxRetries);
                cancellationToken.ThrowIfCancellationRequested();
                actualHash = runtime.ComputeSha1(file.SavePath, cancellationToken);
                if (actualHash != file.Hash)
                {
                    s_logger.Error($"{file.DisplayName} 在重新下载后仍校验失败");
                    throw new InvalidOperationException($"SHA1 mismatch for {file.DisplayName}");
                }
            }
        }

        return FileProcessResult.Downloaded;
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
                // 忽略创建失败，继续执行
            }
        }
    }

    /// <summary>
    /// 优化的SHA1计算：使用同步方式但分块读取，减少异步开销
    /// </summary>
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
        
        // 分块读取，避免大文件一次性加载
        var buffer = new byte[bufferSize];

        int bytesRead;
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            bytesRead = stream.Read(buffer, 0, buffer.Length);
            if (bytesRead <= 0)
                break;

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

    internal async static Task DownloadFileAsync(string url, string savePath, CancellationToken cancellationToken, int maxRetries = 3)
    {
        Exception? lastException = null;
        
        for (int retry = 0; retry <= maxRetries; retry++)
        {
            try
            {
                var response = await HttpUtils.CreateRequest(url)
                    .WithRetry(3)
                    .WithRetryDelay(1000)
                    .GetAsync(cancellationToken);

                response.EnsureSuccessStatusCode();

                await using var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken);
                await using var fileStream = File.Create(savePath);
                await contentStream.CopyToAsync(fileStream, cancellationToken);
                return;
            }
            catch (Exception ex) when (retry < maxRetries && !cancellationToken.IsCancellationRequested)
            {
                lastException = ex;
                s_logger.Debug($"下载失败 (重试 {retry + 1}/{maxRetries + 1}): {ex.Message}");
                
                if (File.Exists(savePath))
                {
                    try { File.Delete(savePath); }
                    catch
                    {
                        // ignored
                    }
                }
                
                await Task.Delay(500 * (retry + 1), cancellationToken);
            }
        }
        
        throw lastException ?? new InvalidOperationException($"下载 {url} 在 {maxRetries + 1} 次重试后失败");
    }
}
