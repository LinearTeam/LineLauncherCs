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

namespace LMCCore.Game.Download.Vanilla.Batching;

internal sealed record PreparedBatchFile<TFile>(
    TFile Source,
    string SavePath,
    string DownloadUrl,
    string DisplayName,
    long? FileSize,
    string? Hash);

internal sealed class BatchDownloadRuntime
{
    public static BatchDownloadRuntime Default { get; } = new();

    public Func<string, CancellationToken, string> ComputeSha1 { get; init; } = BatchDownloader.ComputeSha1Fast;
    public Func<string, string, CancellationToken, int, Task> DownloadFileAsync { get; init; } = BatchDownloader.DownloadFileAsync;
    public Action<IEnumerable<string>> PreCreateDirectories { get; init; } = BatchDownloader.PreCreateDirectories;
}

internal sealed class BatchDownloadProgressTracker(int totalCount, IProgress<int>? progress)
{
    private const int ProgressReportInterval = 5;
    private readonly int _totalCount = totalCount;
    private readonly IProgress<int>? _progress = progress;
    private readonly object _syncRoot = new();
    private int _downloadedCount;
    private int _failedCount;
    private int _skippedCount;
    private int _lastReportedPercent = -1;
    private DateTime _lastReportTime = DateTime.UtcNow;

    public void RecordDownloaded() => RecordCompletion(ref _downloadedCount);
    public void RecordSkipped() => RecordCompletion(ref _skippedCount);
    public void RecordFailed() => RecordCompletion(ref _failedCount);

    public BatchDownloadResult CreateResult()
    {
        lock (_syncRoot)
        {
            return new BatchDownloadResult(_downloadedCount, _failedCount, _skippedCount);
        }
    }

    public void ReportCompleted()
    {
        lock (_syncRoot)
        {
            if (_lastReportedPercent == 100)
            {
                return;
            }

            _lastReportedPercent = 100;
            _lastReportTime = DateTime.UtcNow;
            _progress?.Report(100);
        }
    }

    private void RecordCompletion(ref int counter)
    {
        lock (_syncRoot)
        {
            counter++;
            TryReportProgress();
        }
    }

    private void TryReportProgress()
    {
        var currentPercent = (_downloadedCount + _skippedCount + _failedCount) * 100 / _totalCount;
        var now = DateTime.UtcNow;

        if (currentPercent - _lastReportedPercent >= ProgressReportInterval ||
            (now - _lastReportTime).TotalMilliseconds >= 500)
        {
            _lastReportedPercent = currentPercent;
            _lastReportTime = now;
            _progress?.Report(currentPercent);
        }
    }
}

internal static class BatchDownloadPreparation
{
    public static IReadOnlyList<PreparedBatchFile<TFile>> PrepareFiles<TFile>(BatchDownloadOptions<TFile> options)
    {
        return options.Files
            .Select(file => new PreparedBatchFile<TFile>(
                file,
                options.GetSavePath(file),
                options.GetDownloadUrl(file),
                options.GetDisplayName?.Invoke(file) ?? Path.GetFileName(options.GetSavePath(file)),
                options.GetFileSize?.Invoke(file),
                options.GetHash?.Invoke(file)))
            .ToList();
    }

    public static IReadOnlyList<string> CollectDirectories<TFile>(IEnumerable<PreparedBatchFile<TFile>> files)
    {
        return files
            .Select(file => Path.GetDirectoryName(file.SavePath))
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Cast<string>()
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}

internal static class BatchDownloadSkipEvaluator
{
    public static string? GetSkipReason<TFile>(
        PreparedBatchFile<TFile> file,
        BatchDownloadOptions<TFile> options,
        BatchDownloadRuntime runtime,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var existingSize = new FileInfo(file.SavePath).Length;

        if (!file.FileSize.HasValue && string.IsNullOrEmpty(file.Hash))
        {
            return options.SkipIfSizeMatches ? "文件已存在" : null;
        }

        if (file.FileSize.HasValue)
        {
            if (existingSize != file.FileSize.Value)
            {
                return null;
            }

            if (string.IsNullOrEmpty(file.Hash))
            {
                return options.SkipIfSizeMatches ? "大小匹配" : null;
            }

            var fileHash = runtime.ComputeSha1(file.SavePath, cancellationToken);
            return fileHash == file.Hash ? "SHA1匹配" : null;
        }

        if (!string.IsNullOrEmpty(file.Hash))
        {
            var fileHash = runtime.ComputeSha1(file.SavePath, cancellationToken);
            return fileHash == file.Hash ? "SHA1匹配" : null;
        }

        return null;
    }
}
