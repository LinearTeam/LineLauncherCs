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
using System.Net.Http;
using System.Net.Sockets;
using System.Buffers;
using System.Security.Cryptography;
using LMC.Basic.Logging;
using LMCCore.Utils;

namespace LMCCore.Game.Modpacks;

internal static class ModpackFileDownloader
{
    private static readonly Logger s_logger = new("ModpackDownload");
    private static readonly HashSet<string> s_sources = new(StringComparer.OrdinalIgnoreCase)
    {
        "cdn.modrinth.com", "github.com", "raw.githubusercontent.com", "gitlab.com"
    };
    private static readonly HashSet<string> s_redirectHosts = new(StringComparer.OrdinalIgnoreCase)
    {
        "release-assets.githubusercontent.com", "objects.githubusercontent.com",
        "gitlab-static.net", "storage.googleapis.com"
    };
    private static readonly HttpClient s_client = new(new SocketsHttpHandler
    {
        AllowAutoRedirect = false,
        UseCookies = false,
        UseProxy = false,
        ConnectTimeout = TimeSpan.FromSeconds(15),
        ConnectCallback = ConnectAsync
    });

    internal static Uri ValidateUrl(string url, bool isRedirect)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps
            || !uri.IsDefaultPort || uri.UserInfo.Length != 0 || uri.Fragment.Length != 0
            || (!s_sources.Contains(uri.IdnHost) && (!isRedirect || !s_redirectHosts.Contains(uri.IdnHost))))
            throw new InvalidDataException($"Untrusted modpack download URL: {url}");
        return uri;
    }

    internal static bool IsPublicAddress(IPAddress address)
    {
        if (address.IsIPv4MappedToIPv6)
            address = address.MapToIPv4();
        if (IPAddress.IsLoopback(address))
            return false;
        var bytes = address.GetAddressBytes();
        if (address.AddressFamily == AddressFamily.InterNetworkV6)
            return (bytes[0] & 0xE0) == 0x20
                && !(bytes[0] == 0x20 && bytes[1] == 0x01 && bytes[2] == 0x0D && bytes[3] == 0xB8);
        return bytes[0] is not (0 or 10 or 127) && bytes[0] < 224
            && !(bytes[0] == 100 && bytes[1] is >= 64 and <= 127)
            && !(bytes[0] == 169 && bytes[1] == 254)
            && !(bytes[0] == 172 && bytes[1] is >= 16 and <= 31)
            && !(bytes[0] == 192 && bytes[1] is 0 or 168)
            && !(bytes[0] == 198 && bytes[1] is 18 or 19);
    }

    private static async ValueTask<Stream> ConnectAsync(
        SocketsHttpConnectionContext context, CancellationToken cancellationToken)
    {
        var addresses = await Dns.GetHostAddressesAsync(context.DnsEndPoint.Host, cancellationToken)
            .ConfigureAwait(false);
        if (addresses.Length == 0 || addresses.Any(address => !IsPublicAddress(address)))
            throw new HttpRequestException("Modpack download resolved to a non-public network address.");
        Exception? lastError = null;
        foreach (var address in addresses)
        {
            var socket = new Socket(address.AddressFamily, SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };
            try
            {
                await socket.ConnectAsync(new IPEndPoint(address, context.DnsEndPoint.Port), cancellationToken)
                    .ConfigureAwait(false);
                return new NetworkStream(socket, ownsSocket: true);
            }
            catch (Exception ex)
            {
                socket.Dispose();
                cancellationToken.ThrowIfCancellationRequested();
                lastError = ex;
            }
        }
        throw new HttpRequestException("Unable to connect to the modpack download host.", lastError);
    }

    public static async Task DownloadAsync(
        ModrinthModpackFile file, string destinationPath, CancellationToken cancellationToken)
    {
        Exception? lastError = null;
        foreach (var candidate in file.Downloads)
        {
            for (var attempt = 0; attempt < 2; attempt++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var temporaryPath = destinationPath + $".part-{Guid.NewGuid():N}";
                try
                {
                    using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                    timeout.CancelAfter(TimeSpan.FromMinutes(10));
                    var uri = ValidateUrl(candidate, isRedirect: false);
                    using var response = await GetResponseAsync(uri, timeout.Token).ConfigureAwait(false);
                    response.EnsureSuccessStatusCode();
                    if (response.Content.Headers.ContentLength is { } length && length != file.FileSize)
                        throw new InvalidDataException($"Unexpected content length for {file.Path}.");
                    Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);
                    await using (var input = await response.Content.ReadAsStreamAsync(timeout.Token).ConfigureAwait(false))
                    await using (var output = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write,
                        FileShare.None, 81920, FileOptions.Asynchronous))
                        await ModpackPaths.CopyExactAsync(input, output, file.FileSize, timeout.Token)
                            .ConfigureAwait(false);
                    await ValidateFileAsync(temporaryPath, file, timeout.Token).ConfigureAwait(false);
                    cancellationToken.ThrowIfCancellationRequested();
                    File.Move(temporaryPath, destinationPath, overwrite: true);
                    return;
                }
                catch (Exception ex) when (ex is IOException or InvalidDataException
                                           or HttpRequestException or OperationCanceledException)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    lastError = ex;
                    s_logger.Debug($"Failed modpack file '{file.Path}' from '{candidate}' ({attempt + 1}/2): {ex.Message}");
                    if (attempt == 0)
                        await Task.Delay(500, cancellationToken).ConfigureAwait(false);
                }
                finally
                {
                    if (File.Exists(temporaryPath))
                        File.Delete(temporaryPath);
                }
            }
        }
        throw new IOException($"Failed to download modpack file: {file.Path}", lastError);
    }

    private static async Task<HttpResponseMessage> GetResponseAsync(Uri uri, CancellationToken cancellationToken)
    {
        for (var redirects = 0; redirects <= 5; redirects++)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, uri);
            request.Headers.UserAgent.ParseAdd(HttpUtils.ProjectUserAgent);
            var response = await s_client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                .ConfigureAwait(false);
            if (response.StatusCode is not (HttpStatusCode.MovedPermanently or HttpStatusCode.Redirect
                or HttpStatusCode.SeeOther or HttpStatusCode.TemporaryRedirect or HttpStatusCode.PermanentRedirect))
                return response;
            var location = response.Headers.Location;
            response.Dispose();
            if (location == null)
                throw new HttpRequestException("Download redirect has no destination.");
            uri = ValidateUrl(new Uri(uri, location).AbsoluteUri, isRedirect: true);
        }
        throw new HttpRequestException("Too many modpack download redirects.");
    }

    internal static async Task ValidateFileAsync(string path, ModrinthModpackFile file,
        CancellationToken cancellationToken)
    {
        await using var input = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read,
            81920, FileOptions.Asynchronous | FileOptions.SequentialScan);
        if (input.Length != file.FileSize)
            throw new InvalidDataException($"File size mismatch: {file.Path}");
        // Both hashes describe the same file; neither is an alternative to the other.
        using var sha1Hasher = IncrementalHash.CreateHash(HashAlgorithmName.SHA1);
        using var sha512Hasher = IncrementalHash.CreateHash(HashAlgorithmName.SHA512);
        var buffer = ArrayPool<byte>.Shared.Rent(81920);
        try
        {
            int read;
            while ((read = await input.ReadAsync(buffer.AsMemory(), cancellationToken).ConfigureAwait(false)) != 0)
            {
                sha1Hasher.AppendData(buffer, 0, read);
                sha512Hasher.AppendData(buffer, 0, read);
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
        var sha1 = Convert.ToHexString(sha1Hasher.GetHashAndReset());
        var sha512 = Convert.ToHexString(sha512Hasher.GetHashAndReset());
        if (!sha1.Equals(file.Hashes["sha1"], StringComparison.OrdinalIgnoreCase)
            || !sha512.Equals(file.Hashes["sha512"], StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException($"File hash mismatch: {file.Path}");
    }
}
