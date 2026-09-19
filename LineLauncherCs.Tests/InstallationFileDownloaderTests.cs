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
using LMCCore.Game.Download.Installation;
using LMCCore.Utils;

namespace LineLauncherCs.Tests;

public class InstallationFileDownloaderTests : IDisposable
{
    [Fact]
    public async Task DownloadFileWithFallbackAsync_UsesNextSourceAfterPrimaryRetriesFail()
    {
        using var scope = new TestFileSystemScope();
        var savePath = scope.GetPath("forge-installer.jar");
        var requestedUrls = new List<string>();

        HttpUtils.Transport = new DelegateHttpRequestTransport((request, _) =>
        {
            var url = request.RequestUri!.ToString();
            requestedUrls.Add(url);

            return url switch
            {
                "https://mirror.example.com/forge-installer.jar" => throw new HttpRequestException("mirror failed"),
                "https://official.example.com/forge-installer.jar" => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("payload", Encoding.UTF8, "application/java-archive")
                }),
                _ => throw new InvalidOperationException($"Unexpected request: {url}")
            };
        });

        await InstallationFileDownloader.DownloadFileWithFallbackAsync(
            [
                "https://mirror.example.com/forge-installer.jar",
                "https://official.example.com/forge-installer.jar"
            ],
            savePath,
            CancellationToken.None,
            maxRetriesPerSource: 1);

        Assert.Equal(
        [
            "https://mirror.example.com/forge-installer.jar",
            "https://mirror.example.com/forge-installer.jar",
            "https://official.example.com/forge-installer.jar"
        ], requestedUrls);
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
