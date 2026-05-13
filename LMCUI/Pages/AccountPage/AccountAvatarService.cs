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

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using LMC;
using LMC.Basic.Logging;
using LMCCore.Account.Model;
using LMCCore.Account.OAuth;

namespace LMCUI.Pages.AccountPage;

internal static class AccountAvatarService
{
    private readonly static Logger s_logger = new("AccountAvatarService");
    private readonly static string s_cacheDirectory = Path.Combine(Current.LMCPath, "cache", "account-avatars");

    private const string DefaultAvatarBase64 =
        "data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAACAAAAAgCAYAAABzenr0AAABjklEQVR4AezWwUvCUBwH8K+D0tRVRGRJqVBE1MF754LA/oA6WZgQGBRdjeoW1M2gwEtQl24dMgqC6NKhQ4EXo0seFExBihpIzbbWyW87tNOah218YI9tj/e+v21vQjgkqmwsIKosHOxQ2Ui/R2VDvS6VDfvdKuO+fo5HB0SVCbB4swcg1BWA6StSVxUwj6sVLNDlBuvr9IA58HtXtP6YXYLmS2AnEQVLrcTBdleXwZLzc2DrC1GwzZlpMG+bE6z5EtC/BWa3rU9gIzoLpsgOsOqLBFaqVMHypQqYIiva/Q2v729gixPjYNYnYHaNjfq3PgG30wkWSabAjq8HwVzyJ1jQ1wO2n/GBxfZOwdq7/WCCUURmn7cHIDznH8EyazGw+GQZ7OzmFuzk/ApsKVIAO0pMgeVy92B2CaxPQJK/wArFJ7DtgzRYyCviL1vpQ7CqVANzoEX7S2ywPgGzPzRG/VufQL5U1tbzhodiBUw/g4vsHdhlLgumv16qac8Y+dDWEmZ9AvoR/3fb9ASMJvQNAAD//zii3k4AAAAGSURBVAMAKieGVEUw3nEAAAAASUVORK5CYII=";

    public static void ApplyCachedOrDefaultAvatar(Account account)
    {
        if (TryReadCachedAvatarBase64(account.Uuid, out var cachedAvatar))
        {
            account.AvatarBase64 = cachedAvatar;
            s_logger.Info($"账号 {account.Name} 命中本地头像缓存");
            return;
        }

        account.AvatarBase64 = DefaultAvatarBase64;
        s_logger.Info($"账号 {account.Name} 未命中头像缓存，使用默认头像");
    }

    public async static Task<bool> TryUpdateMicrosoftAvatarAsync(MicrosoftAccount account, CancellationToken cancellationToken = default)
    {
        ApplyCachedOrDefaultAvatar(account);
        s_logger.Info($"开始获取微软账号头像: {account.Name}");

        try
        {
            var skinUrlResult = await MicrosoftOAuth.GetActiveSkinUrlAsync(account, cancellationToken);
            if (skinUrlResult.exception != null || string.IsNullOrWhiteSpace(skinUrlResult.skinUrl))
            {
                if (skinUrlResult.exception != null)
                {
                    s_logger.Debug($"Getting active skin url for {account.Name} failed: {skinUrlResult.exception}");
                }
                s_logger.Info($"获取微软账号皮肤地址失败，保留当前头像: {account.Name}");
                return false;
            }

            var cachedSkinUrl = ReadCachedSkinUrl(account.Uuid);
            if (string.Equals(cachedSkinUrl, skinUrlResult.skinUrl, StringComparison.OrdinalIgnoreCase) &&
                TryReadCachedAvatarBase64(account.Uuid, out var cachedAvatar))
            {
                account.AvatarBase64 = cachedAvatar;
                s_logger.Info($"微软账号 {account.Name} 的皮肤地址未变化，继续使用缓存头像");
                return false;
            }

            var avatarBytes = await DownloadAvatarBytesAsync(skinUrlResult.skinUrl, cancellationToken);
            if (avatarBytes == null || avatarBytes.Length == 0)
            {
                s_logger.Info($"微软账号 {account.Name} 的头像下载或裁剪失败，保留当前头像");
                return false;
            }

            WriteCache(account.Uuid, skinUrlResult.skinUrl, avatarBytes);
            account.AvatarBase64 = ToDataUri(avatarBytes);
            s_logger.Info($"微软账号 {account.Name} 的头像已更新并写入缓存");
            return true;
        }
        catch (OperationCanceledException)
        {
            s_logger.Info($"微软账号头像刷新已取消: {account.Name}");
            throw;
        }
        catch (Exception ex)
        {
            s_logger.Debug($"Updating avatar for microsoft account {account.Name} failed: {ex}");
            s_logger.Info($"微软账号头像刷新失败，保留当前头像: {account.Name}");
            return false;
        }
    }

    async private static Task<byte[]?> DownloadAvatarBytesAsync(string skinUrl, CancellationToken cancellationToken)
    {
        try
        {
            var response = await LMCCore.Utils.HttpUtils.CreateRequest(skinUrl)
                .WithRetry(2)
                .WithRetryDelay(500)
                .GetAsync(cancellationToken);
            response.EnsureSuccessStatusCode();

            var skinBytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);
            using var input = new MemoryStream(skinBytes);
            using var skinBitmap = new Bitmap(input);

            var scale = Math.Max(1, skinBitmap.PixelSize.Width / 64);
            if (skinBitmap.PixelSize.Width < 32 || skinBitmap.PixelSize.Height < 16 * scale)
            {
                return null;
            }

            var headSize = 8 * scale;
            var baseRect = new PixelRect(8 * scale, 8 * scale, headSize, headSize);
            var overlayRect = new PixelRect(40 * scale, 8 * scale, headSize, headSize);
            var hasOverlay = skinBitmap.PixelSize.Width >= overlayRect.Right &&
                             skinBitmap.PixelSize.Height >= overlayRect.Bottom;

            using var baseHead = new CroppedBitmap(skinBitmap, baseRect);
            using var output = new RenderTargetBitmap(new PixelSize(headSize, headSize));
            using (var context = output.CreateDrawingContext())
            {
                var sourceRect = new Rect(0, 0, baseHead.Size.Width, baseHead.Size.Height);
                var destRect = new Rect(0, 0, output.Size.Width, output.Size.Height);
                baseHead.Draw(context, sourceRect, destRect);
                if (hasOverlay)
                {
                    using var overlayHead = new CroppedBitmap(skinBitmap, overlayRect);
                    overlayHead.Draw(context, sourceRect, destRect);
                }
            }

            var scaledSize = Math.Max(32, headSize * 4);
            using var scaledOutput = output.CreateScaledBitmap(
                new PixelSize(scaledSize, scaledSize),
                BitmapInterpolationMode.None);
            using var avatarStream = new MemoryStream();
            scaledOutput.Save(avatarStream);
            return avatarStream.ToArray();
        }
        catch (Exception ex)
        {
            s_logger.Debug($"Downloading or rendering avatar from skin {skinUrl} failed: {ex}");
            return null;
        }
    }

    private static void WriteCache(string uuid, string skinUrl, byte[] avatarBytes)
    {
        Directory.CreateDirectory(s_cacheDirectory);
        File.WriteAllText(GetSkinUrlCachePath(uuid), skinUrl);
        File.WriteAllBytes(GetAvatarCachePath(uuid), avatarBytes);
        s_logger.Info($"已写入头像缓存: {NormalizeUuid(uuid)}");
    }

    private static string? ReadCachedSkinUrl(string uuid)
    {
        var path = GetSkinUrlCachePath(uuid);
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            return File.ReadAllText(path);
        }
        catch (Exception ex)
        {
            s_logger.Debug($"Reading cached skin url for {uuid} failed: {ex}");
            return null;
        }
    }

    private static bool TryReadCachedAvatarBase64(string uuid, out string base64)
    {
        var path = GetAvatarCachePath(uuid);
        if (!File.Exists(path))
        {
            base64 = string.Empty;
            return false;
        }

        try
        {
            base64 = ToDataUri(File.ReadAllBytes(path));
            return true;
        }
        catch (Exception ex)
        {
            s_logger.Debug($"Reading cached avatar for {uuid} failed: {ex}");
            base64 = string.Empty;
            return false;
        }
    }

    private static string GetAvatarCachePath(string uuid) =>
        Path.Combine(s_cacheDirectory, $"{NormalizeUuid(uuid)}.png");

    private static string GetSkinUrlCachePath(string uuid) =>
        Path.Combine(s_cacheDirectory, $"{NormalizeUuid(uuid)}.url");

    private static string NormalizeUuid(string uuid) =>
        uuid.Replace("-", "", StringComparison.Ordinal).ToLowerInvariant();

    private static string ToDataUri(byte[] pngBytes) =>
        $"data:image/png;base64,{Convert.ToBase64String(pngBytes)}";
}
