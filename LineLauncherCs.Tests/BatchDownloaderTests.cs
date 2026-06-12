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
using System.Text;
using LMCCore.Game.Download.Vanilla;
using LMCCore.Game.Download.Vanilla.Batching;
using LMCCore.Utils;

namespace LineLauncherCs.Tests;

public class BatchDownloaderTests : IDisposable
{
    [Fact]
    public async Task DownloadAsync_ReturnsZeroCountsForEmptyInput()
    {
        var result = await BatchDownloader.DownloadAsync(
            new BatchDownloadOptions<string>
            {
                Files = [],
                GetSavePath = file => file,
                GetDownloadUrl = file => file
            },
            CancellationToken.None);

        Assert.Equal(new BatchDownloadResult(0, 0, 0), result);
    }

    [Fact]
    public async Task DownloadAsync_SkipsExistingFileWhenSizeMatches()
    {
        using var scope = new TestFileSystemScope();
        var savePath = scope.GetPath("existing.bin");
        await File.WriteAllBytesAsync(savePath, [1, 2, 3]);

        var result = await BatchDownloader.DownloadAsync(
            new BatchDownloadOptions<string>
            {
                Files = ["file-1"],
                GetSavePath = _ => savePath,
                GetDownloadUrl = _ => "https://example.com/file",
                GetFileSize = _ => 3
            },
            CancellationToken.None,
            progress: null,
            runtime: new BatchDownloadRuntime
            {
                DownloadFileAsync = (_, _, _, _) => throw new InvalidOperationException("Should not download")
            });

        Assert.Equal(new BatchDownloadResult(0, 0, 1), result);
    }

    [Fact]
    public async Task DownloadAsync_TracksDownloadedFailedAndSkippedCounts()
    {
        using var scope = new TestFileSystemScope();
        var skippedPath = scope.GetPath("skipped.bin");
        await File.WriteAllBytesAsync(skippedPath, [1, 2, 3]);
        var downloadedPath = scope.GetPath("downloaded.bin");
        var failedPath = scope.GetPath("failed.bin");

        var result = await BatchDownloader.DownloadAsync(
            new BatchDownloadOptions<string>
            {
                Files = ["skipped", "downloaded", "failed"],
                GetSavePath = file => file switch
                {
                    "skipped" => skippedPath,
                    "downloaded" => downloadedPath,
                    _ => failedPath
                },
                GetDownloadUrl = file => $"https://example.com/{file}",
                GetFileSize = file => file == "skipped" ? 3 : null
            },
            CancellationToken.None,
            progress: null,
            runtime: new BatchDownloadRuntime
            {
                DownloadFileAsync = async (url, path, _, _) =>
                {
                    if (url.EndsWith("/failed", StringComparison.Ordinal))
                    {
                        throw new InvalidOperationException("boom");
                    }

                    await File.WriteAllTextAsync(path, "ok");
                }
            });

        Assert.Equal(new BatchDownloadResult(1, 1, 1), result);
        Assert.True(File.Exists(downloadedPath));
    }

    [Fact]
    public async Task DownloadAsync_RespectsMaxConcurrency()
    {
        using var scope = new TestFileSystemScope();
        var concurrentDownloads = 0;
        var maxConcurrentDownloads = 0;

        var result = await BatchDownloader.DownloadAsync(
            new BatchDownloadOptions<int>
            {
                Files = [1, 2, 3],
                MaxConcurrency = 1,
                GetSavePath = file => scope.GetPath($"{file}.bin"),
                GetDownloadUrl = file => $"https://example.com/{file}"
            },
            CancellationToken.None,
            progress: null,
            runtime: new BatchDownloadRuntime
            {
                DownloadFileAsync = async (_, path, _, _) =>
                {
                    var current = Interlocked.Increment(ref concurrentDownloads);
                    maxConcurrentDownloads = Math.Max(maxConcurrentDownloads, current);
                    await Task.Delay(30);
                    await File.WriteAllTextAsync(path, "ok");
                    Interlocked.Decrement(ref concurrentDownloads);
                }
            });

        Assert.Equal(1, maxConcurrentDownloads);
        Assert.Equal(new BatchDownloadResult(3, 0, 0), result);
    }

    [Fact]
    public async Task DownloadAsync_ReportsFinalProgressOf100()
    {
        using var scope = new TestFileSystemScope();
        var progressValues = new List<int>();

        var result = await BatchDownloader.DownloadAsync(
            new BatchDownloadOptions<int>
            {
                Files = [1, 2],
                GetSavePath = file => scope.GetPath($"{file}.bin"),
                GetDownloadUrl = file => $"https://example.com/{file}"
            },
            CancellationToken.None,
            new Progress<int>(value => progressValues.Add(value)),
            new BatchDownloadRuntime
            {
                DownloadFileAsync = async (_, path, _, _) => await File.WriteAllTextAsync(path, "ok")
            });

        Assert.True(SpinWait.SpinUntil(() => progressValues.Contains(100), TimeSpan.FromSeconds(3)));
        Assert.Equal(new BatchDownloadResult(2, 0, 0), result);
        Assert.Equal(100, Assert.Single(progressValues, value => value == 100));
    }

    [Fact]
    public async Task DownloadFileAsync_RetriesBeforeSuccess()
    {
        using var scope = new TestFileSystemScope();
        var savePath = scope.GetPath("download.bin");
        var attempts = 0;
        HttpUtils.Transport = new DelegateHttpRequestTransport((_, _) =>
        {
            attempts++;
            if (attempts < 3)
            {
                throw new HttpRequestException("temporary failure");
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("payload", Encoding.UTF8, "text/plain")
            });
        });

        await BatchDownloader.DownloadFileAsync("https://example.com/retry", savePath, CancellationToken.None, maxRetries: 2);

        Assert.Equal(3, attempts);
        Assert.Equal("payload", await File.ReadAllTextAsync(savePath));
    }

    public void Dispose()
    {
        HttpUtils.ResetTransportForTesting();
    }

    private sealed class DelegateHttpRequestTransport(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> sendAsync) : IHttpRequestTransport
    {
        private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _sendAsync = sendAsync;

        public Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return _sendAsync(request, cancellationToken);
        }
    }
}
