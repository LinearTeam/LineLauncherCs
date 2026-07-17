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
using LMCCore.Game.Download;
using LMCCore.Game.Download.Vanilla;
using LMCCore.Game.Launching.Steps.Resources;
using LMCCore.Game.Model.LocalVersion;
using LMCCore.Game.Model.LocalVersion.Libraries;
using LMCCore.Utils;

namespace LineLauncherCs.Tests;

public class VanillaGameDownloaderTests : IDisposable
{
    private const string OfficialManifestUrl = "https://launchermeta.mojang.com/mc/game/version_manifest.json";
    private const string MirrorManifestUrl = "https://bmclapi2.bangbang93.com/mc/game/version_manifest.json";

    public VanillaGameDownloaderTests()
    {
        VanillaGameDownloader.ResetVersionManifestCacheForTesting();
    }

    [Fact]
    public async Task GetVersionManifestAsync_FallsBackToOfficialSourceWhenMirrorRequestFails()
    {
        var requestedUrls = new List<string>();
        HttpUtils.Transport = new DelegateHttpRequestTransport((request, _) =>
        {
            var url = request.RequestUri!.ToString();
            requestedUrls.Add(url);

            return url switch
            {
                MirrorManifestUrl => throw new HttpRequestException("mirror failed"),
                OfficialManifestUrl => Task.FromResult(CreateManifestResponse("1.21.9")),
                _ => throw new InvalidOperationException($"Unexpected request: {url}")
            };
        });

        var downloader = new VanillaGameDownloader(DownloadSourceManager.CreateDefault());

        var manifest = await downloader.GetVersionManifestAsync();

        Assert.Equal("1.21.9", manifest.Latest.Release);
        Assert.Equal(3, requestedUrls.Count(url => url == MirrorManifestUrl));
        Assert.Equal(1, requestedUrls.Count(url => url == OfficialManifestUrl));
        Assert.Equal(MirrorManifestUrl, requestedUrls[0]);
        Assert.Equal(OfficialManifestUrl, requestedUrls[^1]);
    }

    [Fact]
    public async Task GetVersionManifestAsync_DoesNotRequestOfficialSourceWhenMirrorSucceeds()
    {
        var requestedUrls = new List<string>();
        HttpUtils.Transport = new DelegateHttpRequestTransport((request, _) =>
        {
            var url = request.RequestUri!.ToString();
            requestedUrls.Add(url);

            return url switch
            {
                MirrorManifestUrl => Task.FromResult(CreateManifestResponse("1.21.8")),
                OfficialManifestUrl => throw new InvalidOperationException("Official source should not be requested."),
                _ => throw new InvalidOperationException($"Unexpected request: {url}")
            };
        });

        var downloader = new VanillaGameDownloader(DownloadSourceManager.CreateDefault());

        var manifest = await downloader.GetVersionManifestAsync();

        Assert.Equal("1.21.8", manifest.Latest.Release);
        Assert.Equal([MirrorManifestUrl], requestedUrls);
    }

    [Fact]
    public void TryBuildMavenRelativePath_SupportsAtQualifiedExtension()
    {
        var success = VanillaGameDownloader.TryBuildMavenRelativePath(
            "net.minecraft:client:1.20.6:mappings@tsrg",
            out var relativePath);

        Assert.True(success);
        Assert.Equal(
            "net/minecraft/client/1.20.6/client-1.20.6-mappings.tsrg",
            relativePath);
    }

    [Fact]
    public void SimpleLibraryInfo_GetPreferredSha1_UsesChecksumsFallback()
    {
        var library = new SimpleLibraryInfo
        {
            Name = "com.example:demo:1.0.0",
            Checksums = ["abc123", "def456"]
        };

        Assert.Equal("abc123", library.GetPreferredSha1());
    }

    [Fact]
    public void GetLibrariesForDownload_UsesOfficialLibrarySourceForSimpleLibraryWithoutUrl()
    {
        var versionInfo = new LocalVersionInfo
        {
            Id = "legacy",
            MainClass = "net.minecraft.client.main.Main",
            Libraries =
            [
                new SimpleLibraryInfo
                {
                    Name = "java3d:vecmath:1.5.2"
                }
            ]
        };

        var downloads = VanillaGameDownloader.GetLibrariesForDownload(versionInfo);

        var download = Assert.Single(downloads);
        Assert.Equal("java3d/vecmath/1.5.2/vecmath-1.5.2.jar", download.Path);
        Assert.Equal(
            "https://libraries.minecraft.net/java3d/vecmath/1.5.2/vecmath-1.5.2.jar",
            download.Url);
        Assert.True(download.IgnoreNotFound);
    }

    [Fact]
    public void CheckFile_AcceptsAnyMatchingChecksum()
    {
        var filePath = Path.GetTempFileName();
        try
        {
            var bytes = Encoding.UTF8.GetBytes("hello forge");
            File.WriteAllBytes(filePath, bytes);
            var matchingSha1 = ComputeSha1(bytes);

            var result = GameLaunchFileIntegrityHelper.CheckFile(
                filePath,
                sha1: null,
                validHashes: ["deadbeef", matchingSha1],
                size: bytes.Length,
                cancellationToken: CancellationToken.None);

            Assert.True(result.IsValid);
        }
        finally
        {
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
        }
    }

    public void Dispose()
    {
        VanillaGameDownloader.ResetVersionManifestCacheForTesting();
        HttpUtils.ResetTransportForTesting();
    }

    private static HttpResponseMessage CreateManifestResponse(string releaseVersion)
    {
        var json = $$"""
                     {
                       "latest": {
                         "release": "{{releaseVersion}}",
                         "snapshot": "25w01a"
                       },
                       "versions": [
                         {
                           "id": "{{releaseVersion}}",
                           "type": "release",
                           "url": "https://launchermeta.mojang.com/v1/packages/example.json",
                           "time": "2026-01-01T00:00:00Z",
                           "releaseTime": "2026-01-01T00:00:00Z"
                         }
                       ]
                     }
                     """;

        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
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

    private static string ComputeSha1(byte[] bytes)
    {
        using var sha1 = System.Security.Cryptography.SHA1.Create();
        return Convert.ToHexString(sha1.ComputeHash(bytes)).ToLowerInvariant();
    }
}
